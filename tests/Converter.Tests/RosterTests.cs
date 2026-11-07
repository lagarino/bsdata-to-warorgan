using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>Phase 3, stage 3: the effective roster of a catalogue (docs/08 section 1).</summary>
public class RosterTests
{
    private static string[] Names(IEnumerable<ResolvedUnit> units) =>
        units.Select(u => BsdDatabase.Str(u.Entry, "name")!).Order().ToArray();

    [Fact]
    public void Roster_is_the_root_linked_units_and_models_only()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var roster = new RosterResolver(db).Resolve("Orks.json");
        // Boyz (unit) and Warboss (model) are datasheets; the Legends entry is out of scope, the root-linked
        // upgrade is not a unit, and "Never Linked" is a shared entry nothing links from the root.
        Assert.Equal(["Boyz", "Warboss"], Names(roster));
        Assert.All(roster, u => Assert.False(u.Imported));
    }

    private const string Library = """
        { "catalogue": { "id": "cat-lib", "name": "Test Library", "library": true,
          "sharedSelectionEntries": [ { "id": "u-lib", "name": "Library Unit", "type": "unit" } ],
          "catalogueLinks": [ { "targetId": "cat-lib2", "importRootEntries": true } ],
          "entryLinks": [ { "id": "ll", "targetId": "u-lib", "type": "selectionEntry" } ] } }
        """;
    private const string Library2 = """
        { "catalogue": { "id": "cat-lib2", "name": "Second Library",
          "sharedSelectionEntries": [ { "id": "u-lib2", "name": "Second Library Unit", "type": "unit" } ],
          "entryLinks": [ { "id": "ll2", "targetId": "u-lib2", "type": "selectionEntry" } ] } }
        """;
    private const string LegendsLib = """
        { "catalogue": { "id": "cat-leg", "name": "Legends Library",
          "sharedSelectionEntries": [ { "id": "u-leg", "name": "Legends Unit", "type": "unit" } ],
          "entryLinks": [ { "id": "lg", "targetId": "u-leg", "type": "selectionEntry" } ] } }
        """;

    private static string WithLinks(string links) =>
        Fixtures.Orks.Replace("\"type\": \"catalogue\",", "\"type\": \"catalogue\", \"catalogueLinks\": [" + links + "],");

    [Fact]
    public void Imported_catalogues_add_their_root_entries_and_are_flagged()
    {
        var orks = WithLinks("""{ "targetId": "cat-lib", "importRootEntries": true }""");
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", orks), ("Library.json", Library), ("Library2.json", Library2));
        var roster = new RosterResolver(db).Resolve("Orks.json");
        Assert.Equal(["Boyz", "Library Unit", "Warboss"], Names(roster));   // not transitive: no "Second Library Unit"
        Assert.True(roster.Single(u => BsdDatabase.Str(u.Entry, "name") == "Library Unit").Imported);
    }

    [Fact]
    public void Links_without_importRootEntries_and_legends_libraries_add_nothing()
    {
        var orks = WithLinks("""{ "targetId": "cat-lib", "importRootEntries": false }, { "targetId": "cat-leg", "importRootEntries": true }""");
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", orks), ("Library.json", Library), ("Library2.json", Library2), ("Legends Library.json", LegendsLib));
        Assert.Equal(["Boyz", "Warboss"], Names(new RosterResolver(db).Resolve("Orks.json")));
    }
}
