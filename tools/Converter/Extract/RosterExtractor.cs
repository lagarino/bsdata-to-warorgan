using WarOrgan.Model.Unit;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

public sealed record RosterResult(IReadOnlyList<ResolvedUnit> Resolved, List<Unit> Units, HashSet<string> ArmyRules, List<WarOrgan.Model.Faction.Detachment> Detachments);

/// <summary>Everything that is extracted for one catalogue's roster, in the order the stages depend on each other.</summary>
public static class RosterExtractor
{
    /// <summary>
    /// <paramref name="includeImported"/> keeps the units a catalogue imports from allied catalogues (they are allies, not part of the
    /// faction's own roster, so by default they are left out; Space Marine chapters need them and are not supported yet).
    /// </summary>
    public static RosterResult Extract(BsdDatabase db, string catalogueFile, List<string> warnings, bool includeImported = false)
    {
        var resolved = new RosterResolver(db).Resolve(catalogueFile).Where(r => includeImported || !r.Imported).ToList();
        var armyRules = UnitExtractor.FindArmyRules(resolved);
        var extractor = new UnitExtractor(db);
        var units = resolved.Select(r => extractor.Extract(r, armyRules, warnings)).ToList();
        var gated = new LeaderExtractor(db).Apply(resolved, units, warnings);
        var detachments = new DetachmentExtractor(db).Extract(catalogueFile, resolved, units, warnings, gated);
        return new RosterResult(resolved, units, armyRules, detachments);
    }
}
