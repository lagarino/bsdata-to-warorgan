using System.IO.Compression;
using System.Text.Json;
using WarOrgan.Data.BuildDataset.Utils;
using WarOrgan.Model.DataSet;
using WarOrgan.Model.Faction;
using WarOrganConverter.Emit;
using WarOrganConverter.Load;

namespace Converter.Tests;

/// <summary>
/// Phase 3, stage 8: ability regex lists, the dataset definition, builder validation and the zip.
/// Everything that touches the builder's static log lives in this one class so it runs sequentially.
/// </summary>
public class EmitTests
{
    private static readonly Dictionary<string, string> Rules = new()
    {
        ["Anti"] = "Anti text.", ["Anti-Fly"] = "Longer name.", ["Deadly Demise"] = "Boom.", ["Feel No Pain"] = "Ignore.", ["Twin-linked"] = "Twin.",
    };

    // ---- ability definitions ----

    [Fact]
    public void Keywords_are_normalised_unicode_hyphens_placeholders_and_spacing()
    {
        Assert.Equal(["TWIN-LINKED", "ANTI-INFANTRY 4+"], AbilityDefinitions.SplitKeywords("TWIN‑LINKED,  ANTI-INFANTRY   4+ , -, "));
        Assert.Empty(AbilityDefinitions.SplitKeywords("-"));
        Assert.Empty(AbilityDefinitions.SplitKeywords(null));
        Assert.Equal("", AbilityDefinitions.NormaliseKeywords(""));
        Assert.Equal("RAPID FIRE 2, HEAVY", AbilityDefinitions.NormaliseKeywords("RAPID FIRE 2,HEAVY"));
    }

    [Fact]
    public void A_regex_covers_a_rule_name_with_or_without_a_value_and_ignores_case()
    {
        var unmatched = new List<string>();
        var list = AbilityDefinitions.Build(["Deadly Demise D6", "DEADLY DEMISE 1", "Deadly Demise"], Rules, unmatched);
        var one = Assert.Single(list);                                    // three tokens, one rule, one regex
        Assert.Equal("Boom.", one.Description);
        var regex = new System.Text.RegularExpressions.Regex(one.Regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.Matches(regex, "Deadly Demise D3");
        Assert.DoesNotMatch(regex, "Deadly Demises");
        Assert.Empty(unmatched);
    }

    [Fact]
    public void The_shortest_covering_rule_wins_and_brackets_are_ignored()
    {
        var list = AbilityDefinitions.Build(["[Anti-Infantry 4+]"], Rules, []);
        Assert.Equal("Anti text.", Assert.Single(list).Description);       // "Anti", not the longer "Anti-Fly"
    }

    [Fact]
    public void A_spelling_variant_with_a_space_instead_of_a_hyphen_matches_the_same_rule()
    {
        var unmatched = new List<string>();
        var list = AbilityDefinitions.Build(["Twin Linked", "TWIN-LINKED"], Rules, unmatched);
        var one = Assert.Single(list);
        Assert.Empty(unmatched);
        var regex = new System.Text.RegularExpressions.Regex(one.Regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.Matches(regex, "Twin Linked");
        Assert.Matches(regex, "Twin-linked");
    }

    [Fact]
    public void Tokens_without_a_source_rule_get_an_exact_regex_and_are_reported()
    {
        var unmatched = new List<string>();
        var list = AbilityDefinitions.Build(["Mystery Rule"], Rules, unmatched);
        Assert.Equal(["Mystery Rule"], unmatched);
        Assert.Equal(AbilityDefinitions.MissingDescription, Assert.Single(list).Description);
        Assert.Matches(new System.Text.RegularExpressions.Regex(list[0].Regex), "Mystery Rule");
    }

    [Fact]
    public void Display_name_drops_the_category_prefix()
    {
        Assert.Equal("Orks", DatasetBuilder.DisplayName("Xenos - Orks"));
        Assert.Equal("Necrons", DatasetBuilder.DisplayName("Necrons"));
    }

    // ---- whole dataset ----

    private static (DatasetBuildModel Model, BuildReport Report, BuildSettings Settings, BsdDatabase Db) BuildFixture(params string[] catalogues)
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var settings = new BuildSettings("Test Dataset", new Version(1, 2, 3, 4), Path.Combine(Path.GetTempPath(), "bsdata-to-warorgan-tests", Guid.NewGuid().ToString("N")), catalogues);
        var report = new BuildReport();
        return (DatasetBuilder.Build(db, settings, report), report, settings, db);
    }

    [Fact]
    public void The_fixture_dataset_passes_the_builders_own_validation()
    {
        var (model, report, _, _) = BuildFixture();
        var errorsBefore = LoggingBuffer.ErrorCount;
        DatasetBuilder.Validate(model);                                    // throws on any validation error
        Assert.Equal(errorsBefore, LoggingBuffer.ErrorCount);
        Assert.Equal((2, 1), (report.Units, report.Rosters));
    }

    [Fact]
    public void Definition_has_the_faction_battle_sizes_features_and_only_categories_present_in_the_data()
    {
        var (model, _, _, _) = BuildFixture();
        var d = model.Definition;
        var faction = Assert.Single(d.Factions);
        Assert.Equal(("Orks", "Orks", "Test"), (faction.Name, faction.FactionKeyword, faction.Category));
        Assert.Matches("^#[0-9a-fA-F]{6}$", faction.MainColor);
        Assert.Equal(["Character", "Infantry"], d.CategoryKeywords.Select(c => c.Keyword));   // Epic Hero, Monster ... are absent
        Assert.Equal(["Incursion", "Strike Force", "Onslaught"], d.BattleSizeDefinitions.Select(b => b.Name));
        Assert.Equal(DataSetFeatures.ArmyRules, d.Features);
    }

    [Fact]
    public void Ability_lists_cover_every_ability_the_units_use()
    {
        var (model, _, _, _) = BuildFixture();
        var regexes = model.Definition.WeaponAbilities.Concat(model.Definition.CoreAbilities)
            .Select(a => new System.Text.RegularExpressions.Regex(a.Regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
        var units = model.Rosters.SelectMany(r => r.Roster.Units).ToList();
        var used = units.SelectMany(u => u.CoreAbilities)
            .Concat(units.SelectMany(u => u.Weapons).SelectMany(s => s.Weapons).SelectMany(w => AbilityDefinitions.SplitKeywords(w.Keywords))).ToList();
        Assert.NotEmpty(used);
        Assert.All(used, a => Assert.Contains(regexes, r => r.IsMatch(a)));
    }

    [Fact]
    public void Army_rules_come_from_rules_most_units_link_and_unit_keywords_are_normalised()
    {
        var (model, _, _, _) = BuildFixture();
        var roster = model.Rosters.Single().Roster;
        // With two units, a rule linked by one of them is already above the 40% army-rule share.
        Assert.Contains(roster.ArmyRules, r => r.Title == "Waaagh!" && r.Text == "Army rule.");
        Assert.All(roster.Units.SelectMany(u => u.Weapons).SelectMany(s => s.Weapons), w => Assert.NotEqual("", w.Keywords));
    }

    [Fact]
    public void Catalogue_selection_matches_by_name_display_name_or_file_and_rejects_no_match()
    {
        Assert.Single(BuildFixture("Test - Orks").Model.Rosters);
        Assert.Single(BuildFixture("Orks").Model.Rosters);
        Assert.Throws<InvalidOperationException>(() => BuildFixture("No Such Faction"));
    }

    [Fact]
    public void Publish_writes_a_zip_with_definition_info_roster_and_readme_that_load_back_through_the_builders_reader()
    {
        var (model, _, settings, _) = BuildFixture();
        DatasetBuilder.Validate(model);
        var zipPath = DatasetBuilder.Publish(model, settings, "test fixtures");
        Assert.True(File.Exists(zipPath));
        using var zip = ZipFile.OpenRead(zipPath);
        Assert.Equal(["DataSetDefinition.json", "DataSetInfo.json", "Orks.json", "README.md"], zip.Entries.Select(e => e.FullName).Order());

        string Read(string name) { using var s = new StreamReader(zip.GetEntry(name)!.Open()); return s.ReadToEnd(); }
        var info = JsonSerializer.Deserialize<DataSetInfo>(Read("DataSetInfo.json"))!;
        Assert.Equal(("Test Dataset", new Version(1, 2, 3, 4), 1), (info.Id, info.Version, info.DataSetFormatVersion));
        Assert.Contains("do not publish", Read("README.md"));
        Assert.DoesNotContain("Warhammer", Read("README.md"));

        // The builder's own loader rejects unknown members, so this proves the files match the model.
        var dir = Path.Combine(settings.OutputPath, "unzipped");
        zip.ExtractToDirectory(dir);
        Assert.NotNull(IoUtils.LoadRequiredFile<FactionRoster>(Path.Combine(dir, "Orks.json")));
        Assert.NotNull(IoUtils.LoadRequiredFile<DataSetDefinition>(Path.Combine(dir, "DataSetDefinition.json")));
    }
}

public class TextCleanerTests
{
    [Theory]
    [InlineData("a friendly ^^***Necrons***^^ unit", "a friendly Necrons unit")]
    [InlineData("an ^^**Adepta Sororitas Character^^** is", "an Adepta Sororitas Character is")]
    [InlineData("has the [**SUSTAINED HITS 1**] ability", "has the [SUSTAINED HITS 1] ability")]
    [InlineData("INFANTRY\u00a0models", "INFANTRY models")]
    [InlineData("Feel No Pain 3+*", "Feel No Pain 3+*")]   // a single asterisk is content, not markup
    public void Markup_is_removed_from_texts(string text, string expected) => Assert.Equal(expected, WarOrganConverter.Emit.TextCleaner.Clean(text));

    [Fact]
    public void Texts_and_descriptions_deep_in_the_model_are_cleaned_but_other_fields_are_not()
    {
        var unit = new WarOrgan.Model.Unit.Unit { Id = "^^X^^", DisplayName = "**Y**" };
        unit.UnitAbilities.Add(new WarOrgan.Model.Unit.UnitInfoSection { Title = "**T**", Text = "^^**a**^^ b" });
        WarOrganConverter.Emit.TextCleaner.CleanAll(new List<WarOrgan.Model.Unit.Unit> { unit });
        Assert.Equal("a b", unit.UnitAbilities[0].Text);
        Assert.Equal("**T**", unit.UnitAbilities[0].Title);
        Assert.Equal("^^X^^", unit.Id);
    }
}
