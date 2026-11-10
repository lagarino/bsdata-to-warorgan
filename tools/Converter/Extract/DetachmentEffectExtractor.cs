using System.Text.Json.Nodes;
using WarOrgan.Model.Effects;
using WarOrgan.Model.Faction;
using WarOrgan.Model.Unit;
using WarOrganConverter.Emit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>
/// What a selected detachment changes on unit datasheets. In BSData these are modifiers on a unit, model or weapon entry
/// whose only condition is "this detachment is selected" ("atLeast" the detachment, force scope). Understood kinds:
/// weapon characteristic or keyword changes (per weapon type), unit characteristic changes, added ability profiles and added
/// categories. Anything else (list-building limits, hidden options, display annotations) is reported or ignored, never guessed.
/// </summary>
public sealed class DetachmentEffectExtractor
{
    private readonly BsdDatabase _db;
    private readonly EntryWalker _walker;
    private readonly Dictionary<string, (string ProfileType, string Characteristic)> _characteristics = new(StringComparer.Ordinal);

    public DetachmentEffectExtractor(BsdDatabase db)
    {
        _db = db;
        _walker = new EntryWalker(db);
        foreach (var root in db.Roots.Values.Where(r => BsdDatabase.Str(r, "type") == "gameSystem"))
            foreach (var pt in BsdDatabase.Arr(root, "profileTypes"))
                foreach (var c in BsdDatabase.Arr(pt, "characteristicTypes"))
                    if (BsdDatabase.Str(c, "id") is { } id)
                        _characteristics[id] = (BsdDatabase.Str(pt, "name") ?? "", BsdDatabase.Str(c, "name") ?? "");
    }

    private sealed class Effect
    {
        public List<WeaponStatAdjustment> Weapons { get; } = [];
        public List<StatAdjustment> Stats { get; } = [];
        public List<UnitInfoSection> Abilities { get; } = [];
        public List<string> Keywords { get; } = [];
        public int PointsDelta { get; set; }
    }

    public void Apply(IReadOnlyList<ResolvedUnit> resolved, IReadOnlyList<Unit> units, IReadOnlyList<(JsonObject Entry, Detachment Model)> detachments, List<string> warnings)
    {
        var byId = detachments.ToDictionary(d => BsdDatabase.Str(d.Entry, "id")!, d => d.Model);
        var effects = new Dictionary<(string Detachment, int Unit), Effect>();
        var unsupported = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        for (var i = 0; i < resolved.Count; i++)
            Walk(resolved[i].Entry, resolved[i].Entry, i, units[i], byId, effects, unsupported, new HashSet<JsonObject>(ReferenceEqualityComparer.Instance), isRoot: true);

        foreach (var ((detId, unitIndex), e) in effects.OrderBy(k => k.Key.Detachment, StringComparer.Ordinal).ThenBy(k => k.Key.Unit))
        {
            var effect = new DetachmentEffect { AffectedUnits = [units[unitIndex].Id] };
            var weapons = MergeWeaponAdjustments(e.Weapons, units[unitIndex], warnings);
            if (weapons.Count > 0) effect.WeaponStatAdjustments = weapons;
            if (e.Stats.Count > 0) effect.StatAdjustments = e.Stats;
            if (e.Abilities.Count > 0) effect.AddedAbilities = e.Abilities;
            if (e.PointsDelta > 0)
                effect.PointsOverride = units[unitIndex].Points.Select(p => new PointsDefinition { UnitCount = p.UnitCount, ModelCount = p.ModelCount, Cost = p.Cost + e.PointsDelta }).ToList();
            if (e.Keywords.Count > 0)
                effect.DatasheetAdjustments = [new DatasheetAdjustment { Target = DatasheetFeature.Keywords, Type = StatAdjustmentType.Add, Scope = StatAdjustmentScope.Unit, Values = e.Keywords }];
            AddMerged(byId[detId], effect);
        }

        foreach (var (reason, where) in unsupported.OrderBy(k => k.Key, StringComparer.Ordinal))
            warnings.Add($"detachment effect not converted ({reason}): {string.Join(", ", where.Order().Take(6))}{(where.Count > 6 ? $" and {where.Count - 6} more" : "")}");
    }

    /// <summary>One effect for all the units that get exactly the same change (an ability a detachment gives several units).</summary>
    private static void AddMerged(Detachment detachment, DetachmentEffect effect)
    {
        string Key(DetachmentEffect e)
        {
            var units = e.AffectedUnits;
            e.AffectedUnits = null;
            try { return System.Text.Json.JsonSerializer.Serialize(e); }
            finally { e.AffectedUnits = units; }
        }
        var key = Key(effect);
        var same = detachment.Effects.FirstOrDefault(x => Key(x) == key);
        if (same is null) { detachment.Effects.Add(effect); return; }
        foreach (var unit in effect.AffectedUnits ?? [])
            if (!same.AffectedUnits!.Contains(unit)) same.AffectedUnits.Add(unit);
    }

    private void Walk(JsonObject node, JsonObject root, int unitIndex, Unit unit, Dictionary<string, Detachment> byId,
        Dictionary<(string, int), Effect> effects, Dictionary<string, HashSet<string>> unsupported, HashSet<JsonObject> seen, bool isRoot)
    {
        if (!seen.Add(node)) return;
        if (!isRoot && Scope.IsSkippedEntry(BsdDatabase.Str(node, "name"))) return;
        Modifiers(node, node, unitIndex, unit, byId, effects, unsupported);
        foreach (var key in new[] { "selectionEntries", "selectionEntryGroups" })
            foreach (var child in BsdDatabase.Arr(node, key))
                Walk(child, root, unitIndex, unit, byId, effects, unsupported, seen, isRoot: false);
        foreach (var l in BsdDatabase.Arr(node, "entryLinks"))
        {
            if (BsdDatabase.Str(l, "targetId") is not { } id || !_db.ById.TryGetValue(id, out var t) || Scope.IsSkippedEntry(BsdDatabase.Str(t.Node, "name"))) continue;
            Modifiers(l, t.Node, unitIndex, unit, byId, effects, unsupported);   // a modifier on a weapon link affects that weapon
            Walk(t.Node, root, unitIndex, unit, byId, effects, unsupported, seen, isRoot: false);
        }
        foreach (var l in BsdDatabase.Arr(node, "infoLinks"))
        {
            Modifiers(l, node, unitIndex, unit, byId, effects, unsupported);
            if (BsdDatabase.Str(l, "targetId") is { } tid && _db.ById.TryGetValue(tid, out var info))
                AbilityOfDetachment(info.Node, BsdDatabase.Str(l, "name"), unitIndex, byId, effects);
        }
        foreach (var p in BsdDatabase.Arr(node, "profiles").Where(p => BsdDatabase.Str(p, "typeName") == "Abilities"))
            AbilityOfDetachment(p, null, unitIndex, byId, effects);
    }

    /// <summary>
    /// An ability that is hidden unless a detachment is selected ("fewer than 1 of this detachment" hides it) is an ability that
    /// detachment gives the unit.
    /// </summary>
    private void AbilityOfDetachment(JsonObject ability, string? linkName, int unitIndex, Dictionary<string, Detachment> byId, Dictionary<(string, int), Effect> effects)
    {
        foreach (var m in BsdDatabase.Arr(ability, "modifiers"))
        {
            if (BsdDatabase.Str(m, "field") != "hidden" || m["value"]?.ToString() != "true") continue;
            var conditions = AllConditions(m).ToList();
            if (conditions.Count == 0 || !conditions.All(c => BsdDatabase.Str(c, "childId") is { } id && byId.ContainsKey(id)
                    && (BsdDatabase.Str(c, "type") is "lessThan" or "atMost" || BsdDatabase.Str(c, "type") == "equalTo" && c["value"]?.ToString() == "0"))) continue;
            var title = linkName ?? BsdDatabase.Str(ability, "name");
            var text = BsdDatabase.Arr(ability, "characteristics").Select(c => BsdDatabase.Str(c, "$text")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                       ?? BsdDatabase.Str(ability, "description");
            if (title is null || string.IsNullOrWhiteSpace(text)) continue;
            foreach (var detId in conditions.Select(c => BsdDatabase.Str(c, "childId")!).Distinct())
            {
                if (!effects.TryGetValue((detId, unitIndex), out var effect)) effects[(detId, unitIndex)] = effect = new Effect();
                if (!effect.Abilities.Any(a => a.Title == title.Trim())) effect.Abilities.Add(new UnitInfoSection { Title = title.Trim(), Text = text.Trim() });
            }
        }
    }

    /// <summary>Handles the modifiers of <paramref name="holder"/>; <paramref name="scope"/> is the entry whose profiles they change.</summary>
    private void Modifiers(JsonObject holder, JsonObject scope, int unitIndex, Unit unit, Dictionary<string, Detachment> byId,
        Dictionary<(string, int), Effect> effects, Dictionary<string, HashSet<string>> unsupported)
    {
        // An option that is hidden by default and shown (and made mandatory) by a detachment is something that detachment adds.
        var unlocked = ModifiersOf(holder)
            .Where(x => BsdDatabase.Str(x.Modifier, "field") == "hidden" && BsdDatabase.Str(x.Modifier, "type") == "set"
                        && x.Modifier["value"] is JsonValue hv && hv.TryGetValue<bool>(out var hidden) && !hidden)
            .SelectMany(x => AllConditions(x.Modifier).Concat(x.Inherited))
            .Select(c => BsdDatabase.Str(c, "childId")).Where(id => id is not null && byId.ContainsKey(id)).Cast<string>().ToHashSet();
        foreach (var (m, inherited) in ModifiersOf(holder))
        {
            var conditions = AllConditions(m).Concat(inherited).ToList();
            var detachmentConditions = conditions.Where(c => BsdDatabase.Str(c, "childId") is { } id && byId.ContainsKey(id)).ToList();
            if (detachmentConditions.Count == 0) continue;
            var field = BsdDatabase.Str(m, "field") ?? "";
            var type = BsdDatabase.Str(m, "type") ?? "";

            void Report(string reason) => (unsupported.TryGetValue(reason, out var set) ? set : unsupported[reason] = []).Add(unit.DisplayName ?? unit.Id);

            if (field == "annotation") continue;                                       // display only
            if (field == "hidden" && type == "set" && m["value"] is JsonValue unhide && unhide.TryGetValue<bool>(out var hiddenValue) && !hiddenValue)
            {
                foreach (var detId in detachmentConditions.Select(c => BsdDatabase.Str(c, "childId")!).Distinct())
                {
                    if (!effects.TryGetValue((detId, unitIndex), out var unlockEffect)) effects[(detId, unitIndex)] = unlockEffect = new Effect();
                    Unlock(scope, unlockEffect, Report);
                }
                continue;
            }
            if (conditions.Any(c => BsdDatabase.Str(c, "childId") is { } cid && unlocked.Contains(cid)) && !_characteristics.ContainsKey(field)
                && field is not ("add-info" or "category" or "hidden")) continue;       // min/max limits of an unlocked option
            var simple = conditions.Count == detachmentConditions.Count
                         && detachmentConditions.All(c => BsdDatabase.Str(c, "type") is "atLeast" or "instanceOf" or "greaterThan");
            if (!simple) { Report($"{FieldLabel(field)} {type} only under extra conditions or when the detachment is not selected"); continue; }

            foreach (var detId in detachmentConditions.Select(c => BsdDatabase.Str(c, "childId")!).Distinct())
            {
                var key = (detId, unitIndex);
                if (!effects.TryGetValue(key, out var effect)) effects[key] = effect = new Effect();
                if (!Convert(m, field, type, scope, unit, effect, Report)) continue;
            }
        }
    }

    /// <summary>
    /// A weapon that has both a ranged and a melee profile is selected by one id, so the same change for both types must be
    /// written once (it would be applied twice otherwise). Different changes to one stat of such a weapon cannot be told apart.
    /// </summary>
    internal static List<WeaponStatAdjustment> MergeWeaponAdjustments(List<WeaponStatAdjustment> adjustments, Unit unit, List<string> warnings)
    {
        var merged = new List<WeaponStatAdjustment>();
        foreach (var group in adjustments.GroupBy(a => (a.TargetStatId, a.Type, a.Value)))
        {
            var first = group.First();
            merged.Add(new WeaponStatAdjustment
            {
                TargetStatId = first.TargetStatId, Type = first.Type, Value = first.Value, Scope = first.Scope,
                NameFilter = group.SelectMany(a => a.NameFilter ?? []).Distinct().ToList(),
            });
        }
        foreach (var stat in merged.GroupBy(a => a.TargetStatId).Where(g => g.Count() > 1))
            foreach (var clash in stat.SelectMany(a => a.NameFilter!).GroupBy(n => n).Where(g => g.Count() > 1))
                warnings.Add($"detachment effect on {unit.DisplayName ?? unit.Id}: weapon '{clash.Key}' gets different {stat.Key} changes for its ranged and melee profiles; applied both");
        return merged;
    }

    private bool Convert(JsonObject m, string field, string type, JsonObject scope, Unit unit, Effect effect, Action<string> report)
    {
        var value = m["value"]?.ToString() ?? "";
        if (_characteristics.TryGetValue(field, out var ch))
        {
            if (ch.ProfileType is "Ranged Weapons" or "Melee Weapons")
            {
                var ids = _walker.Profiles(scope, ch.ProfileType)
                    .Select(f => (BsdDatabase.Str(f.Owner, "name") ?? BsdDatabase.Str(f.Profile, "name") ?? "").Trim().ToLowerInvariant())
                    .Where(id => id.Length > 0).Distinct().ToList();
                if (ids.Count == 0) return false;
                var adj = new WeaponStatAdjustment { TargetStatId = ch.Characteristic, Scope = StatAdjustmentScope.Unit, NameFilter = ids };
                switch (ch.Characteristic == "Keywords" ? "kw:" + type : type)
                {
                    case "kw:append": adj.Type = StatAdjustmentType.Add; adj.Value = AbilityDefinitions.NormaliseKeywords(value); break;
                    case "increment": adj.Type = StatAdjustmentType.Add; adj.Value = value; break;
                    case "decrement": adj.Type = StatAdjustmentType.Subtract; adj.Value = value; break;
                    case "set": adj.Type = StatAdjustmentType.Set; adj.Value = value; break;
                    case "multiply" when value == "2": adj.Type = StatAdjustmentType.Double; adj.Value = ""; break;
                    default: report($"{ch.ProfileType} {ch.Characteristic} {type}"); return false;
                }
                if (adj.Value.Length == 0 && adj.Type != StatAdjustmentType.Double) return false;
                if (!effect.Weapons.Any(w => w.TargetStatId == adj.TargetStatId && w.Type == adj.Type && w.Value == adj.Value && w.NameFilter!.SequenceEqual(ids)))
                    effect.Weapons.Add(adj);
                return true;
            }
            if (ch.ProfileType == "Unit")
            {
                var names = _walker.Profiles(scope, "Unit").Select(f => BsdDatabase.Str(f.Profile, "name")!).Distinct().ToList();
                if (names.Count == 0) return false;
                var adj = new StatAdjustment { TargetStatId = ch.Characteristic, Scope = StatAdjustmentScope.Unit, Value = value };
                // A modifier on one model only changes that model's statline when the unit has several.
                if (unit.StatLines.Count > 1 && names.Count < unit.StatLines.Count) adj.NameFilter = names;
                switch (type)
                {
                    case "increment": adj.Type = StatAdjustmentType.Add; break;
                    case "decrement": adj.Type = StatAdjustmentType.Subtract; break;
                    case "set": adj.Type = StatAdjustmentType.Set; break;
                    case "multiply" when value == "2": adj.Type = StatAdjustmentType.Double; adj.Value = ""; break;
                    default: report($"unit {ch.Characteristic} {type}"); return false;
                }
                if (!effect.Stats.Any(s => s.TargetStatId == adj.TargetStatId && s.Type == adj.Type && s.Value == adj.Value)) effect.Stats.Add(adj);
                return true;
            }
            report($"{ch.ProfileType} {ch.Characteristic} {type}");
            return false;
        }

        if (field == "add-info" && type == "add" && _db.ById.TryGetValue(value, out var info))
        {
            var title = BsdDatabase.Str(info.Node, "name")?.Trim();
            var text = BsdDatabase.Arr(info.Node, "characteristics").Select(c => BsdDatabase.Str(c, "$text")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                       ?? BsdDatabase.Str(info.Node, "description");
            if (string.IsNullOrEmpty(title) || string.IsNullOrWhiteSpace(text)) return false;
            if (!effect.Abilities.Any(a => a.Title == title)) effect.Abilities.Add(new UnitInfoSection { Title = title, Text = text.Trim() });
            return true;
        }
        if (field == "category" && type is "add" or "set-primary")
        {
            var name = _db.Name(value)?.Trim();
            if (string.IsNullOrEmpty(name) || name.StartsWith("Faction: ", StringComparison.Ordinal)) return false;
            if (!effect.Keywords.Contains(name)) effect.Keywords.Add(name);
            return true;
        }
        report($"{FieldLabel(field)} {type}");
        return false;
    }

    /// <summary>An option a detachment adds: its ability profiles become added abilities and its points cost raises the unit's cost.</summary>
    private void Unlock(JsonObject option, Effect effect, Action<string> report)
    {
        var cost = BsdDatabase.Arr(option, "costs").Where(c => BsdDatabase.Str(c, "typeId") == PointsExtractor.PtsTypeId)
            .Select(c => c["value"] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0).FirstOrDefault();
        effect.PointsDelta += Math.Max(0, cost);
        var added = false;
        foreach (var p in BsdDatabase.Arr(option, "profiles").Where(p => BsdDatabase.Str(p, "typeName") == "Abilities"))
        {
            var title = BsdDatabase.Str(p, "name")?.Trim();
            var text = BsdDatabase.Arr(p, "characteristics").Select(c => BsdDatabase.Str(c, "$text")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (string.IsNullOrEmpty(title) || string.IsNullOrWhiteSpace(text)) continue;
            if (!effect.Abilities.Any(a => a.Title == title)) effect.Abilities.Add(new UnitInfoSection { Title = title, Text = text.Trim() });
            added = true;
        }
        if (BsdDatabase.Arr(option, "profiles").Any(p => BsdDatabase.Str(p, "typeName") is "Ranged Weapons" or "Melee Weapons"))
            report($"option '{BsdDatabase.Str(option, "name")}' unlocked by a detachment brings weapons");
        else if (!added && cost == 0) report($"option '{BsdDatabase.Str(option, "name")}' unlocked by a detachment has no ability or cost");
    }

    private static string FieldLabel(string field) => field.Length > 12 && field.Count(c => c == '-') >= 3 ? "list-building limit" : field;

    private static IEnumerable<(JsonObject Modifier, List<JsonObject> Inherited)> ModifiersOf(JsonObject holder)
    {
        foreach (var m in BsdDatabase.Arr(holder, "modifiers")) yield return (m, []);
        foreach (var g in BsdDatabase.Arr(holder, "modifierGroups"))
            foreach (var x in InGroup(g, [])) yield return x;
    }

    private static IEnumerable<(JsonObject, List<JsonObject>)> InGroup(JsonObject group, List<JsonObject> inherited)
    {
        var here = inherited.Concat(AllConditions(group)).ToList();
        foreach (var m in BsdDatabase.Arr(group, "modifiers")) yield return (m, here);
        foreach (var g in BsdDatabase.Arr(group, "modifierGroups"))
            foreach (var x in InGroup(g, here)) yield return x;
    }

    private static IEnumerable<JsonObject> AllConditions(JsonObject node)
    {
        foreach (var c in BsdDatabase.Arr(node, "conditions")) yield return c;
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups").Concat(BsdDatabase.Arr(node, "localConditionGroups")))
            foreach (var c in AllConditions(g)) yield return c;
    }
}
