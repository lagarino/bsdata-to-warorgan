using System.Text.Json.Nodes;
using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>Builds the statlines and weapons of a unit from its BSData profiles (docs/03-mapping.md).</summary>
public sealed class UnitExtractor(BsdDatabase db)
{
    private readonly EntryWalker _walker = new(db);

    public Unit Extract(ResolvedUnit resolved, List<string> warnings)
    {
        var entry = resolved.Entry;
        var name = BsdDatabase.Str(entry, "name") ?? "UNKNOWN";
        var unit = new Unit { Id = name.ToUpperInvariant(), DisplayName = name };
        unit.StatLines.AddRange(StatLines(entry));
        unit.Weapons.AddRange(Weapons(entry));
        if (unit.StatLines.Count == 0) warnings.Add($"{name}: no unit profile found");
        return unit;
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
