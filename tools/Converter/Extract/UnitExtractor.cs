using System.Text.Json.Nodes;
using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>Builds the statlines and weapons of a unit from its BSData profiles (docs/03-mapping.md).</summary>
public sealed class UnitExtractor(BsdDatabase db)
{
    private readonly EntryWalker _walker = new(db);

    /// <summary>Rules linked by more than this share of a faction's units are army rules, not unit abilities (docs/08 section 4).</summary>
    public const double ArmyRuleShare = 0.4;

    /// <summary>Game-system rules that are modelled elsewhere (leader attachment) and so are not core abilities.</summary>
    private static readonly HashSet<string> NotCoreAbilities = new(StringComparer.Ordinal) { "Leader", "Support", "Crucible" };

    public Unit Extract(ResolvedUnit resolved, ISet<string> armyRuleIds, List<string> warnings)
    {
        var entry = resolved.Entry;
        var name = BsdDatabase.Str(entry, "name") ?? "UNKNOWN";
        var unit = new Unit { Id = name.ToUpperInvariant(), DisplayName = name };
        unit.StatLines.AddRange(StatLines(entry));
        unit.Weapons.AddRange(Weapons(entry));
        Keywords(entry, unit);
        Abilities(entry, unit, armyRuleIds);
        if (unit.StatLines.Count == 0) warnings.Add($"{name}: no unit profile found");
        return unit;
    }

    /// <summary>Rule ids linked by the roster's units, with the number of units linking each.</summary>
    public static HashSet<string> FindArmyRules(IReadOnlyList<ResolvedUnit> units)
    {
        var counts = new Dictionary<string, int>();
        foreach (var u in units)
            foreach (var id in BsdDatabase.Arr(u.Entry, "infoLinks").Where(l => BsdDatabase.Str(l, "type") == "rule").Select(l => BsdDatabase.Str(l, "targetId")).OfType<string>().Distinct())
                counts[id] = counts.GetValueOrDefault(id) + 1;
        return counts.Where(kv => kv.Value > units.Count * ArmyRuleShare).Select(kv => kv.Key).ToHashSet();
    }

    /// <summary>Category links: "Faction: X" becomes a faction keyword, every other category a unit keyword.</summary>
    private void Keywords(JsonObject entry, Unit unit)
    {
        foreach (var l in BsdDatabase.Arr(entry, "categoryLinks"))
        {
            var name = db.Name(BsdDatabase.Str(l, "targetId"))?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (name.StartsWith("Faction: ", StringComparison.Ordinal))
            {
                var f = name["Faction: ".Length..].Trim();
                if (!unit.FactionKeywords.Contains(f)) unit.FactionKeywords.Add(f);
            }
            else if (!unit.Keywords.Contains(name)) unit.Keywords.Add(name);
        }
    }

    /// <summary>
    /// Rules owned by the game system become core abilities (name plus an unconditional appended value, e.g. "Deadly Demise D6");
    /// "Abilities" profiles and unit-specific rules become UnitAbilities. Army rules are skipped here.
    /// </summary>
    private void Abilities(JsonObject entry, Unit unit, ISet<string> armyRuleIds)
    {
        foreach (var l in BsdDatabase.Arr(entry, "infoLinks"))
        {
            var tid = BsdDatabase.Str(l, "targetId");
            if (tid is null || !db.ById.TryGetValue(tid, out var t)) continue;
            var type = BsdDatabase.Str(l, "type");
            if (type == "rule")
            {
                if (armyRuleIds.Contains(tid)) continue;
                var name = BsdDatabase.Str(t.Node, "name") ?? "";
                if (db.Roots.TryGetValue(t.File, out var owner) && BsdDatabase.Str(owner, "type") == "gameSystem")
                {
                    if (NotCoreAbilities.Contains(name)) continue;
                    foreach (var m in BsdDatabase.Arr(l, "modifiers"))
                        if (BsdDatabase.Str(m, "field") == "name" && BsdDatabase.Str(m, "type") == "append"
                            && m["conditions"] is null && m["conditionGroups"] is null && m["value"] is { } v)
                            name += " " + v.ToString();
                    if (!unit.CoreAbilities.Contains(name)) unit.CoreAbilities.Add(name);
                }
                else AddInfo(unit.UnitAbilities, BsdDatabase.Str(l, "name") ?? name, BsdDatabase.Str(t.Node, "description"));
            }
            else if (type == "profile" && BsdDatabase.Str(t.Node, "typeName") == "Abilities")
                AddInfo(unit.UnitAbilities, BsdDatabase.Str(t.Node, "name"), ProfileText(t.Node));
        }
        foreach (var p in BsdDatabase.Arr(entry, "profiles").Where(p => BsdDatabase.Str(p, "typeName") == "Abilities"))
            AddInfo(unit.UnitAbilities, BsdDatabase.Str(p, "name"), ProfileText(p));
    }

    private static string? ProfileText(JsonObject profile) =>
        BsdDatabase.Arr(profile, "characteristics").Select(c => BsdDatabase.Str(c, "$text")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

    private static void AddInfo(List<UnitInfoSection> list, string? title, string? text)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text)) return;
        if (list.Any(a => a.Title == title.Trim())) return;
        list.Add(new UnitInfoSection { Title = title.Trim(), Text = text.Trim() });
    }

    private IEnumerable<StatLine> StatLines(JsonObject entry)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in _walker.Profiles(entry, "Unit"))
        {
            var profileName = BsdDatabase.Str(f.Profile, "name") ?? "";
            if (!seen.Add(profileName)) continue;
            var line = new StatLine { StatName = profileName };
            foreach (var c in Characteristics(f.Profile))
            {
                if (c.Id == "Base") continue; // base size: BaseInfo is handled separately
                line.Stats.Add(c);
            }
            yield return line;
        }
    }

    private IEnumerable<WeaponSet> Weapons(JsonObject entry)
    {
        var sets = new Dictionary<string, WeaponSet>(StringComparer.Ordinal);
        foreach (var f in _walker.Profiles(entry, "Ranged Weapons", "Melee Weapons"))
        {
            var ownerName = BsdDatabase.Str(f.Owner, "name") ?? BsdDatabase.Str(f.Profile, "name") ?? "UNKNOWN";
            var profileName = BsdDatabase.Str(f.Profile, "name") ?? ownerName;
            var chars = Characteristics(f.Profile).ToList();
            var keywords = chars.FirstOrDefault(c => c.Id == "Keywords")?.Value;
            chars.RemoveAll(c => c.Id == "Keywords");
            if (!sets.TryGetValue(ownerName, out var set))
                sets[ownerName] = set = new WeaponSet { Id = ownerName.ToUpperInvariant(), DisplayName = ownerName };
            if (set.Weapons.Any(w => w.DisplayName == profileName)) continue; // same weapon on several models
            set.Weapons.Add(new Weapon
            {
                Id = ownerName.ToLowerInvariant(),
                DisplayName = profileName,
                Keywords = string.IsNullOrWhiteSpace(keywords) ? null : keywords,
                Stats = chars,
            });
        }
        return sets.Values;
    }

    private static IEnumerable<Characteristic> Characteristics(JsonObject profile)
    {
        foreach (var c in BsdDatabase.Arr(profile, "characteristics"))
        {
            var id = BsdDatabase.Str(c, "name");
            var value = BsdDatabase.Str(c, "$text")?.Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(value)) continue;
            yield return new Characteristic { Id = id, Value = value };
        }
    }
}
