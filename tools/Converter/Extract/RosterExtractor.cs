using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

public sealed record RosterResult(IReadOnlyList<ResolvedUnit> Resolved, List<Unit> Units, HashSet<string> ArmyRules, List<WarOrgan.Model.Faction.Detachment> Detachments,
    Dictionary<string, List<string>>? ImportedByFaction = null, string? ParentKeyword = null);

/// <summary>Everything that is extracted for one catalogue's roster, in the order the stages depend on each other.</summary>
public static class RosterExtractor
{
    /// <summary>
    /// A unit imported from another catalogue is part of this faction's own roster when it carries the faction's own keyword (a
    /// library that belongs to the faction); otherwise it is an allied unit. Allied units are left out of the roster and reported
    /// per faction keyword, as unit ids, in <see cref="RosterResult.ImportedByFaction"/>. <paramref name="includeImported"/> keeps
    /// every imported unit in the roster.
    /// </summary>
    public static RosterResult Extract(BsdDatabase db, string catalogueFile, List<string> warnings, bool includeImported = false)
    {
        var all = new RosterResolver(db).Resolve(catalogueFile);
        // The faction's own keyword is the one named like the catalogue ("Xenos - Orks" and "Faction: Orks"); failing that, the most common one.
        var catalogueName = BsdDatabase.Str(db.Roots[catalogueFile], "name") ?? "";
        var shortName = (catalogueName.LastIndexOf(" - ", StringComparison.Ordinal) is var i and >= 0 ? catalogueName[(i + 3)..] : catalogueName).Trim();
        var ownKeyword = all.SelectMany(r => FactionKeywords(db, r.Entry)).FirstOrDefault(k => k.Equals(shortName, StringComparison.OrdinalIgnoreCase))
            ?? LibraryKeyword(db, all, shortName)
            ?? all.Where(r => !r.Imported).SelectMany(r => FactionKeywords(db, r.Entry)).GroupBy(k => k)
                .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        // A sub-faction (a chapter of a parent army): most of its own datasheets carry another faction's keyword next to its own, and the
        // catalogue imports the parent's units. The parent's units are then part of its roster.
        var own = all.Where(r => !r.Imported).ToList();
        var parentKeyword = own.SelectMany(r => FactionKeywords(db, r.Entry)).Where(k => k != ownKeyword).GroupBy(k => k)
            .Where(g => g.Count() * 2 >= own.Count && all.Any(r => r.Imported && FactionKeywords(db, r.Entry).Contains(g.Key)))
            .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault()
            // or most of the roster is another faction's units (a sub-faction with few datasheets of its own)
            ?? all.Where(r => r.Imported).SelectMany(r => FactionKeywords(db, r.Entry).Take(1)).Where(k => k != ownKeyword).GroupBy(k => k)
                .Where(g => g.Count() * 2 >= all.Count).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        var resolved = all.Where(r => !r.Imported || includeImported || (ownKeyword is not null && FactionKeywords(db, r.Entry).Contains(ownKeyword))
                                      || (parentKeyword is not null && FactionKeywords(db, r.Entry).Contains(parentKeyword))).ToList();
        var imported = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var r in all.Except(resolved))
            if (FactionKeywords(db, r.Entry).FirstOrDefault() is { } keyword)
            {
                if (!imported.TryGetValue(keyword, out var ids)) imported[keyword] = ids = [];
                ids.Add((BsdDatabase.Str(r.Entry, "name") ?? "UNKNOWN").ToUpperInvariant());
            }
        var armyRules = UnitExtractor.FindArmyRules(resolved);
        var extractor = new UnitExtractor(db);
        var units = resolved.Select(r => extractor.Extract(r, armyRules, warnings)).ToList();
        var gated = new LeaderExtractor(db).Apply(resolved, units, warnings);
        new LeaderGrantExtractor(db).Apply(resolved, units, warnings);
        var detachments = new DetachmentExtractor(db).Extract(catalogueFile, resolved, units, warnings, gated,
            parentKeyword is null ? null : resolved.Where(r => r.Imported && FactionKeywords(db, r.Entry).Contains(parentKeyword)).Select(r => r.OwnerFile).ToHashSet());
        return new RosterResult(resolved, units, armyRules, detachments, imported, parentKeyword);
    }

    private static List<string> FactionKeywords(BsdDatabase db, System.Text.Json.Nodes.JsonObject entry) =>
        BsdDatabase.Arr(entry, "categoryLinks").Select(l => db.Name(BsdDatabase.Str(l, "targetId"))?.Trim())
            .Where(n => n is not null && n.StartsWith("Faction: ", StringComparison.Ordinal)).Select(n => n!["Faction: ".Length..].Trim()).ToList();

    /// <summary>The keyword of the units of a library named after the catalogue ("Chaos Daemons" and "Chaos - Chaos Daemons Library").</summary>
    private static string? LibraryKeyword(BsdDatabase db, IReadOnlyList<ResolvedUnit> all, string shortName) =>
        all.Where(r => r.Imported && Path.GetFileNameWithoutExtension(r.OwnerFile).Contains(shortName + " Library", StringComparison.OrdinalIgnoreCase))
            .SelectMany(r => FactionKeywords(db, r.Entry)).GroupBy(k => k).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
}
