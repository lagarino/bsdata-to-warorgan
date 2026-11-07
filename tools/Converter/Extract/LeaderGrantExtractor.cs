using System.Text.Json.Nodes;
using WarOrgan.Model.Effects;
using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>
/// Core abilities that a unit has only while a particular leader is attached (the ability link is hidden when "fewer than 1" of that
/// leader is attached to it). WarOrgan keeps this on the leader: <see cref="LeaderInfo.Abilities"/> adds the ability to the unit it leads.
/// </summary>
public sealed class LeaderGrantExtractor(BsdDatabase db)
{
    public void Apply(IReadOnlyList<ResolvedUnit> resolved, IReadOnlyList<Unit> units, List<string> warnings)
    {
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < resolved.Count; i++)
            if (BsdDatabase.Str(resolved[i].Entry, "id") is { } id) indexById[id] = i;

        // (leader index, ability) -> indexes of the units that have it while that leader is attached
        var grants = new Dictionary<(int Leader, string Ability), List<int>>();
        for (var i = 0; i < resolved.Count; i++)
        {
            foreach (var link in BsdDatabase.Arr(resolved[i].Entry, "infoLinks"))
            {
                if (BsdDatabase.Str(link, "type") != "rule" || BsdDatabase.Str(link, "targetId") is not { } rid || !db.ById.TryGetValue(rid, out var rule)) continue;
                if (!db.Roots.TryGetValue(rule.File, out var owner) || BsdDatabase.Str(owner, "type") != "gameSystem") continue;
                var name = BsdDatabase.Str(rule.Node, "name") ?? "";
                foreach (var m in BsdDatabase.Arr(link, "modifiers"))
                    if (BsdDatabase.Str(m, "field") == "name" && BsdDatabase.Str(m, "type") == "append" && m["conditions"] is null && m["conditionGroups"] is null && m["value"] is { } v)
                        name += " " + v;
                foreach (var leaderId in LeadersThatGrant(link, rule.Node))
                    if (indexById.TryGetValue(leaderId, out var li) && units[li].LeaderInfo is not null)
                    {
                        if (!grants.TryGetValue((li, name), out var list)) grants[(li, name)] = list = [];
                        list.Add(i);
                    }
            }
        }

        foreach (var ((li, name), targets) in grants)
        {
            var leader = units[li];
            var info = leader.LeaderInfo!;
            var led = info.UnitNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var all = led.Count > 0 && led.IsSubsetOf(targets.Select(t => units[t].Id));
            // A keyword that exactly the granted units share among the units the leader can lead picks them all together.
            var sharedKeyword = all ? null : targets.Select(t => units[t].Keywords.AsEnumerable()).Aggregate((a, b) => a.Intersect(b)).FirstOrDefault(k =>
                units.Where(u => led.Contains(u.Id) && u.Keywords.Contains(k)).Select(u => u.Id).ToHashSet().SetEquals(targets.Select(t => units[t].Id)));
            // Every unit the leader can lead gets it: no unit has to be named. Otherwise the unit is picked by a keyword of its own name.
            var selectors = all ? [null] : sharedKeyword is not null ? [sharedKeyword] : targets.Select(t => units[t]).Select(u => (string?)(u.Keywords.Contains(u.DisplayName ?? u.Id) ? u.DisplayName ?? u.Id : null)).ToList();
            if (!all && sharedKeyword is null)
                foreach (var t in targets.Where(t => !units[t].Keywords.Contains(units[t].DisplayName ?? units[t].Id)))
                    warnings.Add($"{units[t].DisplayName}: gains {name} while led by {leader.DisplayName}; the unit has no keyword of its own name to select it by, left out");
            foreach (var keyword in selectors.Distinct())
            {
                if (!all && keyword is null) continue;
                var title = all ? $"{name} for led units" : $"{name} for {keyword} units";
                if (!leader.Infos.Any(x => x.Title == title))
                    leader.Infos.Add(new UnitInfoSection { Title = title, Text = all ? $"While this model leads a unit, that unit has {name}." : $"While this model leads a unit with the {keyword} keyword, that unit has {name}." });
                (info.Abilities ??= []).Add(new LeaderAbilityRef
                {
                    AbilityId = title,
                    RequiredUnitKeywords = keyword is null ? null : [keyword],
                    DatasheetAdjustments = [new DatasheetAdjustment { Target = DatasheetFeature.CoreAbilities, Type = StatAdjustmentType.Add, Scope = StatAdjustmentScope.BodyguardUnitOnly, Values = [name] }],
                });
            }
        }
    }

    /// <summary>Entry ids of the leaders whose presence shows the link: hidden when none of them is attached.</summary>
    private static IEnumerable<string> LeadersThatGrant(JsonObject link, JsonObject target)
    {
        foreach (var m in BsdDatabase.Arr(link, "modifiers").Concat(BsdDatabase.Arr(target, "modifiers")))
        {
            if (BsdDatabase.Str(m, "field") != "hidden" || m["value"]?.ToString() != "true") continue;
            var conditions = new List<JsonObject>();
            var andOnly = Collect(m, conditions);
            if (!andOnly || conditions.Count == 0) continue;
            if (!conditions.All(c => BsdDatabase.Str(c, "field") == "associations" && BsdDatabase.Str(c, "type") == "lessThan" && c["value"]?.ToString() == "1")) continue;
            foreach (var c in conditions)
                if (BsdDatabase.Str(c, "childId") is { } id) yield return id;
        }
    }

    /// <summary>Gathers the conditions below a node; false when an "or" group makes them alternatives of hiding.</summary>
    private static bool Collect(JsonObject node, List<JsonObject> into)
    {
        foreach (var c in BsdDatabase.Arr(node, "conditions")) into.Add(c);
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups"))
        {
            if (BsdDatabase.Str(g, "type") == "or") return false;
            if (!Collect(g, into)) return false;
        }
        return BsdDatabase.Str(node, "type") != "or" || BsdDatabase.Arr(node, "conditions").Count() <= 1;
    }
}
