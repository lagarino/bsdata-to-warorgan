using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WarOrgan.Model.Faction;
using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>
/// Detachments (cost, force dispositions, rule text) and their enhancements (docs/08-phase1-findings.md sections 2, 3, 12).
/// The layout differs per catalogue, so everything is located by structure:
/// detachments are the "upgrade" entries below an entry or group named "Detachment", enhancements the "upgrade" entries below
/// groups named "Enhancements" or "Enhancements - Upgrades". An enhancement belongs to the detachment its visibility
/// condition mentions ("detachment not selected" hides it). Detachment effects that change unit data are not converted yet.
/// </summary>
public sealed class DetachmentExtractor(BsdDatabase db)
{
    public const string DetachmentPointsTypeId = "82ae-1066-5107-6ae0";
    private const string PtsTypeId = PointsExtractor.PtsTypeId;

    private static readonly string[] Dispositions = ["Disruption", "Priority Assets", "Purge the Foe", "Take and Hold", "Reconnaissance"];

    private sealed record Eligibility(List<string> Required, List<string> OneOf, List<string> Excluded)
    {
        public Eligibility() : this([], [], []) { }
    }

    public List<Detachment> Extract(string catalogueFile, IReadOnlyList<ResolvedUnit> resolved, IReadOnlyList<Unit> units, List<string> warnings)
    {
        var root = db.Roots[catalogueFile];
        var detachments = new List<(JsonObject Entry, Detachment Model)>();

        foreach (var container in Named(root, "Detachment"))
            foreach (var entry in UpgradeEntries(container, []))
            {
                var name = BsdDatabase.Str(entry.Node, "name") ?? "?";
                if (IsBoardingOnly(entry.Node)) continue;
                var model = BuildDetachment(entry.Node, warnings);
                if (detachments.Any(d => d.Model.Name == model.Name)) continue;
                detachments.Add((entry.Node, model));
            }

        // category id -> roster units carrying it (names a unit or becomes a keyword)
        var carriers = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < resolved.Count; i++)
            foreach (var l in BsdDatabase.Arr(resolved[i].Entry, "categoryLinks"))
                if (BsdDatabase.Str(l, "targetId") is { } cid)
                    (carriers.TryGetValue(cid, out var list) ? list : carriers[cid] = []).Add(i);

        var detachmentIds = detachments.Select(d => BsdDatabase.Str(d.Entry, "id")!).ToHashSet();
        var containerIds = new HashSet<string>();
        foreach (var name in new[] { "Enhancements", "Enhancements - Upgrades" })
            foreach (var container in Named(root, name))
            {
                if (BsdDatabase.Str(container, "id") is { } cid) containerIds.Add(cid);
                var isUpgrade = name.EndsWith("Upgrades", StringComparison.Ordinal);
                foreach (var entry in UpgradeEntries(container, []))
                {
                    var e = BuildEnhancement(entry, isUpgrade, detachmentIds, carriers, resolved, units, warnings, out var owners);
                    if (e is null) continue;
                    if (owners.Count == 0)
                    {
                        warnings.Add($"enhancement '{e.Name}': no detachment found in its visibility conditions; left out");
                        continue;
                    }
                    foreach (var o in owners)
                    {
                        var target = detachments.FirstOrDefault(d => BsdDatabase.Str(d.Entry, "id") == o).Model;
                        if (target is not null && !target.Enhancements.Any(x => x.Name == e.Name)) target.Enhancements.Add(e);
                    }
                }
            }

        // Some enhancements sit inside one unit's own entry (an "Enhancements" group below the datasheet): they are Upgrade
        // enhancements tied to that unit.
        for (var i = 0; i < resolved.Count; i++)
            foreach (var group in EmbeddedEnhancementGroups(resolved[i].Entry, []))
                foreach (var entry in UpgradeEntries(group.Node, group.Chain))
                {
                    var e = BuildEnhancement(entry, true, detachmentIds, carriers, resolved, units, warnings, out var owners, forcedUnit: units[i].Id);
                    if (e is null || owners.Count == 0) continue;
                    foreach (var o in owners)
                    {
                        var target = detachments.FirstOrDefault(d => BsdDatabase.Str(d.Entry, "id") == o).Model;
                        if (target is null) continue;
                        var existing = target.Enhancements.FirstOrDefault(x => x.Name == e.Name);
                        if (existing is null) target.Enhancements.Add(e);
                        else AddUnique(existing.RequiredUnitNames, units[i].Id);
                    }
                }

        new DetachmentEffectExtractor(db).Apply(resolved, units, detachments, warnings);

        MarkUnitsWithoutEnhancements(resolved, units, containerIds);
        return detachments.Select(d => d.Model).ToList();
    }

    // ---- detachments ----

    private Detachment BuildDetachment(JsonObject entry, List<string> warnings)
    {
        var name = BsdDatabase.Str(entry, "name")!.Trim();
        var cost = BsdDatabase.Arr(entry, "costs").Where(c => BsdDatabase.Str(c, "typeId") == DetachmentPointsTypeId)
            .Select(c => c["value"] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0).FirstOrDefault();
        if (cost <= 0)
        {
            warnings.Add($"detachment '{name}': no detachment point cost, using 1");
            cost = 1;
        }
        var categories = BsdDatabase.Arr(entry, "categoryLinks").Select(l => db.Name(BsdDatabase.Str(l, "targetId"))?.Trim()).OfType<string>().ToList();
        var model = new Detachment
        {
            Name = name,
            Cost = cost,
            ForceDispositions = categories.Where(c => Dispositions.Contains(c)).ToList() is { Count: > 0 } d ? d : null,
        };
        model.Rule = BuildRule(entry, name, warnings);
        return model;
    }

    private DetachmentRule? BuildRule(JsonObject entry, string detachmentName, List<string> warnings)
    {
        var parts = new List<(string Title, string Text)>();
        foreach (var r in BsdDatabase.Arr(entry, "rules"))
            if (BsdDatabase.Str(r, "name") is { } n && BsdDatabase.Str(r, "description") is { } t && !string.IsNullOrWhiteSpace(t))
                parts.Add((n.Trim(), t.Trim()));
        foreach (var l in BsdDatabase.Arr(entry, "infoLinks"))
            if (BsdDatabase.Str(l, "type") == "rule" && BsdDatabase.Str(l, "targetId") is { } id && db.ById.TryGetValue(id, out var t)
                && BsdDatabase.Str(t.Node, "description") is { } text && !string.IsNullOrWhiteSpace(text))
                parts.Add(((BsdDatabase.Str(l, "name") ?? BsdDatabase.Str(t.Node, "name"))!.Trim(), text.Trim()));
        if (parts.Count == 0)
        {
            warnings.Add($"detachment '{detachmentName}': no rule text found");
            return null;
        }
        if (parts.Count == 1) return new DetachmentRule { Title = parts[0].Title, Text = parts[0].Text };
        var sb = new StringBuilder();
        foreach (var (title, text) in parts) sb.Append("**").Append(title).Append("**\n").Append(text).Append("\n\n");
        return new DetachmentRule { Title = parts[0].Title, Text = sb.ToString().TrimEnd() };
    }

    /// <summary>Detachments that exist only in Boarding Actions are hidden by "not in Boarding Actions" (docs/08 section 12).</summary>
    private bool IsBoardingOnly(JsonObject entry) =>
        HideModifiers(entry).Any(m => AllConditions(m).Any(c => BsdDatabase.Str(c, "type") == "notInstanceOf"
                                                                && db.Name(BsdDatabase.Str(c, "childId")) is { } n && n.Contains("Boarding Actions", StringComparison.OrdinalIgnoreCase)));

    // ---- enhancements ----

    private Enhancement? BuildEnhancement((JsonObject Node, List<JsonObject> Chain) entry, bool isUpgrade, HashSet<string> detachmentIds,
        Dictionary<string, List<int>> carriers, IReadOnlyList<ResolvedUnit> resolved, IReadOnlyList<Unit> units, List<string> warnings, out List<string> owners, string? forcedUnit = null)
    {
        owners = [];
        var node = entry.Node;
        var name = BsdDatabase.Str(node, "name")?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        var selfId = BsdDatabase.Str(node, "id");

        var text = BsdDatabase.Arr(node, "profiles").Select(p => BsdDatabase.Arr(p, "characteristics").Select(c => BsdDatabase.Str(c, "$text")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)))
            .FirstOrDefault(t => t is not null)
            ?? BsdDatabase.Arr(node, "rules").Select(r => BsdDatabase.Str(r, "description")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        var cost = BsdDatabase.Arr(node, "costs").Where(c => BsdDatabase.Str(c, "typeId") == PtsTypeId)
            .Select(c => c["value"] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0).FirstOrDefault();

        var e = new Enhancement { Name = name, Description = text?.Trim(), Cost = cost, Features = isUpgrade ? EnhancementFeature.Upgrade : EnhancementFeature.None };

        // Which detachment(s) show this enhancement, and which units may take it, from the visibility conditions along the chain.
        var elig = new Eligibility();
        foreach (var holder in entry.Chain.Append(node))
            foreach (var m in HideModifiers(holder))
            {
                foreach (var c in AllConditions(m))
                    if (BsdDatabase.Str(c, "type") == "lessThan" && BsdDatabase.Str(c, "childId") is { } id && detachmentIds.Contains(id) && !owners.Contains(id))
                        owners.Add(id);
                Analyse(m, carriers, elig, selfId, detachmentIds);
            }
        ApplyEligibility(e, elig, carriers, units, warnings, name);
        if (forcedUnit is not null) AddUnique(e.RequiredUnitNames, forcedUnit);

        if (!isUpgrade && text is not null) AddKeywordsFromText(e, text, units);
        if (isUpgrade && e.RequiredUnitNames.Count == 0 && e.RequiredKeywords.Count == 0 && e.RequiredOneOfKeywords.Count == 0 && e.ExcludedKeywords.Count == 0)
            warnings.Add($"upgrade enhancement '{name}': no unit restriction found; it will be offered to every unit when filtering is off");
        return e;
    }

    /// <summary>"**ORKS** **PSYKER** model only." or "NECRONS model only.": the words before "model only" are required keywords.</summary>
    private static void AddKeywordsFromText(Enhancement e, string text, IReadOnlyList<Unit> units)
    {
        var match = Regex.Match(text, @"^\s*([^.:\r\n]{1,80}?)\s+model only", RegexOptions.IgnoreCase);
        if (!match.Success) return;
        var known = units.SelectMany(u => u.Keywords.Concat(u.FactionKeywords)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var words = match.Groups[1].Value.Replace("*", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length;)
        {
            // Longest keyword that starts at this word ("Epic Hero" before "Epic").
            string? hit = null;
            var used = 0;
            for (var len = Math.Min(3, words.Length - i); len >= 1 && hit is null; len--)
            {
                var phrase = string.Join(' ', words.Skip(i).Take(len));
                hit = known.FirstOrDefault(k => k.Equals(phrase, StringComparison.OrdinalIgnoreCase));
                if (hit is not null) used = len;
            }
            if (hit is not null) AddUnique(e.RequiredKeywords, hit);
            i += Math.Max(1, used);
        }
    }

    /// <summary>Reads who may take an enhancement out of a "hidden when ..." modifier (the negation of the eligible units).</summary>
    private void Analyse(JsonObject modifier, Dictionary<string, List<int>> carriers, Eligibility elig, string? selfId, HashSet<string> detachmentIds)
    {
        var groups = BsdDatabase.Arr(modifier, "conditionGroups").Concat(BsdDatabase.Arr(modifier, "localConditionGroups")).ToList();
        var direct = BsdDatabase.Arr(modifier, "conditions").ToList();
        // A modifier's own conditions behave like one "and" group.
        if (direct.Count > 0 || groups.Count > 1) AnalyseGroup("and", direct, groups, carriers, elig, selfId, detachmentIds);
        else if (groups.Count == 1) AnalyseGroup(BsdDatabase.Str(groups[0], "type") ?? "and", BsdDatabase.Arr(groups[0], "conditions").ToList(),
            BsdDatabase.Arr(groups[0], "conditionGroups").Concat(BsdDatabase.Arr(groups[0], "localConditionGroups")).ToList(), carriers, elig, selfId, detachmentIds);
    }

    private void AnalyseGroup(string type, List<JsonObject> conditions, List<JsonObject> groups, Dictionary<string, List<int>> carriers,
        Eligibility elig, string? selfId, HashSet<string> detachmentIds)
    {
        bool IsUnitScope(JsonObject c) => BsdDatabase.Str(c, "scope") is "ancestor" or "root-entry"
                                          && BsdDatabase.Str(c, "childId") is { } id && carriers.ContainsKey(id);
        var unit = conditions.Where(IsUnitScope).ToList();
        if (type == "or")
        {
            // Hidden if any item holds, so every item has to be false for the enhancement to show.
            foreach (var c in unit)
            {
                var id = BsdDatabase.Str(c, "childId")!;
                if (BsdDatabase.Str(c, "type") == "notInstanceOf") elig.Required.Add(id);
                else if (BsdDatabase.Str(c, "type") == "instanceOf") elig.Excluded.Add(id);
            }
            foreach (var g in groups) AnalyseSub(g, carriers, elig, selfId, detachmentIds);
        }
        else
        {
            // Hidden only if every item holds: it shows if any one fails, so "not X and not Y" means X or Y.
            var notIn = unit.Where(c => BsdDatabase.Str(c, "type") == "notInstanceOf").Select(c => BsdDatabase.Str(c, "childId")!).ToList();
            if (notIn.Count == 1) elig.Required.Add(notIn[0]);
            else if (notIn.Count > 1) elig.OneOf.AddRange(notIn);
            foreach (var c in unit.Where(c => BsdDatabase.Str(c, "type") == "instanceOf" && notIn.Count == 0 && conditions.Count(x => !IsUnitScope(x)) == 0))
                elig.Excluded.Add(BsdDatabase.Str(c, "childId")!);
            foreach (var g in groups) AnalyseSub(g, carriers, elig, selfId, detachmentIds);
        }
    }

    private void AnalyseSub(JsonObject group, Dictionary<string, List<int>> carriers, Eligibility elig, string? selfId, HashSet<string> detachmentIds) =>
        AnalyseGroup(BsdDatabase.Str(group, "type") ?? "and", BsdDatabase.Arr(group, "conditions").ToList(),
            BsdDatabase.Arr(group, "conditionGroups").Concat(BsdDatabase.Arr(group, "localConditionGroups")).ToList(), carriers, elig, selfId, detachmentIds);

    private void ApplyEligibility(Enhancement e, Eligibility elig, Dictionary<string, List<int>> carriers, IReadOnlyList<Unit> units, List<string> warnings, string name)
    {
        string Name(string cid) => db.Name(cid)!.Trim();
        foreach (var cid in elig.Required.Distinct())
        {
            if (carriers[cid].Count == 1) AddUnique(e.RequiredUnitNames, units[carriers[cid][0]].Id);
            else AddUnique(e.RequiredKeywords, Name(cid));
        }
        var oneOf = elig.OneOf.Distinct().ToList();
        if (oneOf.Count > 0)
        {
            if (oneOf.All(c => carriers[c].Count == 1)) foreach (var c in oneOf) AddUnique(e.RequiredUnitNames, units[carriers[c][0]].Id);
            else foreach (var c in oneOf) AddUnique(e.RequiredOneOfKeywords, Name(c));
        }
        foreach (var cid in elig.Excluded.Distinct()) AddUnique(e.ExcludedKeywords, Name(cid));
    }

    // ---- units ----

    /// <summary>Characters whose datasheet links no enhancement group cannot take one (UnitFlags.NoEnhancements).</summary>
    private void MarkUnitsWithoutEnhancements(IReadOnlyList<ResolvedUnit> resolved, IReadOnlyList<Unit> units, HashSet<string> containerIds)
    {
        if (containerIds.Count == 0) return;
        for (var i = 0; i < resolved.Count; i++)
        {
            var linked = BsdDatabase.Arr(resolved[i].Entry, "entryLinks").Any(l => BsdDatabase.Str(l, "targetId") is { } id && containerIds.Contains(id));
            var isCharacter = units[i].Keywords.Contains("Character") && !units[i].Keywords.Contains("Epic Hero");
            if (!linked && isCharacter) units[i].Flags |= UnitFlags.NoEnhancements;
        }
    }

    // ---- helpers ----

    private static IEnumerable<JsonObject> Named(JsonObject root, string name) =>
        BsdDatabase.Arr(root, "sharedSelectionEntries").Concat(BsdDatabase.Arr(root, "sharedSelectionEntryGroups"))
            .Where(e => BsdDatabase.Str(e, "name") == name);

    /// <summary>All "upgrade" entries below a node (through nested groups and entries), with the chain of ancestors.</summary>
    private IEnumerable<(JsonObject Node, List<JsonObject> Chain)> UpgradeEntries(JsonObject container, List<JsonObject> chain)
    {
        var here = new List<JsonObject>(chain) { container };
        foreach (var key in new[] { "selectionEntries", "selectionEntryGroups" })
            foreach (var child in BsdDatabase.Arr(container, key))
            {
                if (BsdDatabase.Str(child, "type") == "upgrade") yield return (child, here);
                else
                    foreach (var x in UpgradeEntries(child, here)) yield return x;
            }
    }

    /// <summary>"Enhancements" groups inside a datasheet's own selection tree (not following links).</summary>
    private IEnumerable<(JsonObject Node, List<JsonObject> Chain)> EmbeddedEnhancementGroups(JsonObject node, List<JsonObject> chain)
    {
        foreach (var key in new[] { "selectionEntries", "selectionEntryGroups" })
            foreach (var child in BsdDatabase.Arr(node, key))
            {
                if (Scope.IsSkippedEntry(BsdDatabase.Str(child, "name")) && BsdDatabase.Str(child, "name") != "Enhancements") continue;
                if (BsdDatabase.Str(child, "name") == "Enhancements") yield return (child, chain);
                else
                    foreach (var x in EmbeddedEnhancementGroups(child, [.. chain, child])) yield return x;
            }
    }

    private static IEnumerable<JsonObject> HideModifiers(JsonObject node)
    {
        foreach (var m in BsdDatabase.Arr(node, "modifiers"))
            if (BsdDatabase.Str(m, "field") == "hidden" && m["value"] is JsonValue v && v.TryGetValue<bool>(out var b) && b) yield return m;
        foreach (var g in BsdDatabase.Arr(node, "modifierGroups"))
            foreach (var m in BsdDatabase.Arr(g, "modifiers"))
                if (BsdDatabase.Str(m, "field") == "hidden" && m["value"] is JsonValue v && v.TryGetValue<bool>(out var b) && b) yield return m;
    }

    private static IEnumerable<JsonObject> AllConditions(JsonObject node)
    {
        foreach (var c in BsdDatabase.Arr(node, "conditions")) yield return c;
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups").Concat(BsdDatabase.Arr(node, "localConditionGroups")))
            foreach (var c in AllConditions(g)) yield return c;
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (!list.Contains(value)) list.Add(value);
    }
}
