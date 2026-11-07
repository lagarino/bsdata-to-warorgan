using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>Phase 3, stage 1-2: loading catalogues and applying the scope decisions.</summary>
public class LoaderAndScopeTests
{
    [Fact]
    public void Loads_catalogues_and_indexes_every_id()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        Assert.Equal(2, db.Roots.Count);
        Assert.Equal("Boyz", BsdDatabase.Str(db.ById["u-boyz"].Node, "name"));
        Assert.Equal("Orks.json", db.ById["u-boyz"].File);
        Assert.Equal("Deadly Demise", db.Name("r-dd"));
    }

    [Fact]
    public void First_definition_of_an_id_wins()
    {
        var db = Fixtures.Db(
            ("A.json", """{ "catalogue": { "id": "a", "name": "A", "sharedSelectionEntries": [ { "id": "x", "name": "First" } ] } }"""),
            ("B.json", """{ "catalogue": { "id": "b", "name": "B", "sharedSelectionEntries": [ { "id": "x", "name": "Second" } ] } }"""));
        Assert.Equal("First", db.Name("x"));
    }

    [Fact]
    public void Missing_folder_and_empty_folder_are_errors()
    {
        Assert.Throws<DirectoryNotFoundException>(() => BsdDatabase.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.Throws<InvalidOperationException>(() => Fixtures.Db());
    }

    [Theory]
    [InlineData("Unaligned Forces.json", true)]
    [InlineData("Library - Astartes Heresy Legends.json", true)]
    [InlineData("Orks.json", false)]
    public void Excluded_files(string file, bool excluded) => Assert.Equal(excluded, Scope.IsExcludedFile(file));

    [Theory]
    [InlineData("Old Boss [Legends]", true)]
    [InlineData("Some Hero [Crucible]", true)]
    [InlineData("Warboss", false)]
    [InlineData(null, false)]
    public void Excluded_unit_names(string? name, bool excluded) => Assert.Equal(excluded, Scope.IsExcludedUnitName(name));

    [Theory]
    [InlineData("Crusade", true)]
    [InlineData("Crusade Relic Upgrades", true)]
    [InlineData("Weapon Modifications", true)]
    [InlineData("Enhancements - Upgrades", true)]
    [InlineData("Warlord", true)]
    [InlineData("Wargear", false)]
    [InlineData("Special Weapons", false)]
    [InlineData(null, false)]
    public void Skipped_entries_below_a_unit(string? name, bool skipped) => Assert.Equal(skipped, Scope.IsSkippedEntry(name));
}
