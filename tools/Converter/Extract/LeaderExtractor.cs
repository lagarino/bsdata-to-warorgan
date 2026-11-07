using System.Text.Json.Nodes;
using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>
/// Leader and support attachments (docs/08-phase1-findings.md section 7). An association on a unit lists, as "instanceOf"
/// conditions on categories, the units it may join. A category carried by exactly one unit of the roster names that unit;
/// a category carried by several becomes a keyword the attachment is allowed by.
/// Labels: "Leader" = default leader, "Supported by" = Support, "Retainers" = a unit that must join a unit that already
/// has a leader with a given keyword (the app's RequiredLeaderKeywords rule).
/// Units that may only be joined when an option is selected (atLeast on an upgrade, for example an enhancement) are not in the
/// leader's list; they are returned as <see cref="GatedAttachment"/> so the enhancement can grant them (DatasheetAdjustment
/// LedUnitNames).
/// </summary>
public sealed class LeaderExtractor(BsdDatabase db)
{
    public const string RetainersType = "Retainers";

    /// <summary>
    /// The labelled associations, and the unlabelled "Leading" ones some catalogues use for a plain leader (checked to name units to lead).
    /// </summary>
    private static bool IsAttachment(JsonObject a) => Kind(a) is not null;

    /// <summary>"Leader", "Supported by" or Retainers: catalogues label the same association in several ways ("Leader", "Led by", none).</summary>
    private static string? Kind(JsonObject a)
    {
        var label = BsdDatabase.Str(a, "label");
        var name = BsdDatabase.Str(a, "name");
        if (label == RetainersType) return RetainersType;
        if (a["conditionGroups"] is null && a["conditions"] is null) return null;
        if (BsdDatabase.Str(a, "action") != "group") return label is "Leader" or "Supported by" ? label : null;
        if (name == "Leading" && label is "Leader" or "Led by" or null or "") return "Leader";
        if (name == "Supporting" && label is "Supported by" or "Supported By" or null or "") return "Supported by";
        return label is "Leader" or "Supported by" ? label : null;
    }

    /// <summary>A leader that may join <paramref name="UnitIds"/> only while <paramref name="Option"/> is selected.</summary>
    public sealed record GatedAttachment(int LeaderIndex, List<string> UnitIds, string Option);

    public List<GatedAttachment> Apply(IReadOnlyList<ResolvedUnit> resolved, IReadOnlyList<Unit> units, List<string> warnings)
    {
        var gatedResult = new List<GatedAttachment>();
        // category id -> indexes of the roster units that carry it
        var carriers = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < resolved.Count; i++)
            foreach (var l in BsdDatabase.Arr(resolved[i].Entry, "categoryLinks"))
                if (BsdDatabase.Str(l, "targetId") is { } cid)
                    (carriers.TryGetValue(cid, out var list) ? list : carriers[cid] = []).Add(i);

        for (var i = 0; i < resolved.Count; i++)
        {
            var associations = BsdDatabase.Arr(resolved[i].Entry, "associations")
                .Where(IsAttachment).ToList();
            if (associations.Count == 0) continue;
            if (associations.Count > 1)
                warnings.Add($"{units[i].DisplayName}: {associations.Count} attachment associations; only the first is converted");

            var association = associations[0];
            var label = Kind(association);
            var categories = new List<string>();
            var gated = new List<(string Category, string Option)>();
            Collect(association, carriers, categories, gated, []);

            var info = new LeaderInfo
            {
                LeaderType = label switch { "Supported by" => LeaderInfo.SupportLeaderId, RetainersType => RetainersType, _ => null },
            };
            foreach (var cid in categories.Distinct())
            {
                var members = carriers[cid].Where(m => m != i).ToList();
                if (members.Count == 1) AddUnique(info.UnitNames, units[members[0]].Id);
                else if (members.Count > 1) AddUnique(info.AttachableByKeyword ??= [], db.Name(cid)!.Trim());
            }
            foreach (var group in gated.Distinct().Where(g => !categories.Contains(g.Category)).GroupBy(g => g.Option))
            {
                var ids = group.SelectMany(g => carriers[g.Category].Where(m => m != i).Select(m => units[m].Id)).Distinct().ToList();
                if (ids.Count > 0) gatedResult.Add(new GatedAttachment(i, ids, group.Key));
            }

            if (label == RetainersType)
            {
                // Retainers join a unit that already has a leader with the keyword they name; the units are filled in below.
                info.UnitNames = [];
                info.AttachableByKeyword = null;
                info.RequiredLeaderKeywords = categories.Distinct().Select(c => db.Name(c)!.Trim()).ToList();
            }
            else if (info.UnitNames.Count == 0 && info.AttachableByKeyword is not { Count: > 0 })
            {
                warnings.Add($"{units[i].DisplayName}: attachment lists no unit of this roster; no leader info written");
                continue;
            }
            units[i].LeaderInfo = info;
        }

        // Retainers refer to leaders by keyword: resolve the units those leaders can join once every leader is known.
        for (var i = 0; i < resolved.Count; i++)
        {
            var info = units[i].LeaderInfo;
            if (info?.LeaderType != RetainersType || info.RequiredLeaderKeywords is not { } keywords) continue;
            var names = units.Where(u => u.LeaderInfo is { } li && li.LeaderType != RetainersType && keywords.All(k => u.Keywords.Contains(k)))
                .SelectMany(u => u.LeaderInfo!.UnitNames).Distinct().ToList();
            if (names.Count > 0) info.UnitNames = names;
            else
            {
                warnings.Add($"{units[i].DisplayName}: no leader with keyword {string.Join(" ", keywords)} found to attach to; no leader info written");
                units[i].LeaderInfo = null;
            }
        }
        return gatedResult;
    }

    /// <summary>
    /// Walks the condition tree. Categories named under a condition that needs a selected option are "gated" by that option;
    /// the others are free.
    /// </summary>
    private void Collect(JsonObject node, Dictionary<string, List<int>> carriers, List<string> free, List<(string, string)> gated, List<string> gates)
    {
        var here = new List<string>(gates);
        foreach (var c in BsdDatabase.Arr(node, "conditions"))
            if (BsdDatabase.Str(c, "childId") is { } id && BsdDatabase.Str(c, "type") is "atLeast" or "greaterThan" && !carriers.ContainsKey(id))
                here.Add(db.Name(id) ?? id);
        foreach (var c in BsdDatabase.Arr(node, "conditions"))
        {
            if (BsdDatabase.Str(c, "type") != "instanceOf" || BsdDatabase.Str(c, "childId") is not { } id || !carriers.ContainsKey(id)) continue;
            if (here.Count == 0) free.Add(id);
            else gated.Add((id, string.Join(" + ", here.Distinct())));
        }
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups").Concat(BsdDatabase.Arr(node, "localConditionGroups")))
            Collect(g, carriers, free, gated, here);
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (!list.Contains(value)) list.Add(value);
    }
}
