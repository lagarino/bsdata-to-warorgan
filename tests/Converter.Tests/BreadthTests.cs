using WarOrgan.Model.Unit;
using WarOrgan.Model.Unit.Wargear;
using WarOrganConverter.Extract;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>Shapes found when converting more factions: nested groups, duplicate options, unreachable weapons, loose statlines.</summary>
public class BreadthTests
{
    private static string Weapon(string id, string name, string type = "Melee Weapons") => $$"""
        { "id": "{{id}}", "name": "{{name}}", "type": "upgrade",
          "profiles": [ { "id": "p-{{id}}", "name": "{{name}}", "typeName": "{{type}}", "characteristics": [ { "name": "A", "$text": "1" } ] } ] }
        """;

    private static string Link(string id, string target, string constraints = "") =>
        $$"""{ "id": "{{id}}", "targetId": "{{target}}", "type": "selectionEntry" {{(constraints.Length > 0 ? ", \"constraints\": [" + constraints + "]" : "")}} }""";

    private const string Min1 = """{ "type": "min", "value": 1, "field": "selections", "scope": "parent" }""";

    private static (Unit Unit, List<string> Warnings) Extract(string unitBody, params string[] weapons)
    {
        var json = $$"""
            { "catalogue": { "id": "c", "name": "C",
              "sharedSelectionEntries": [ {{string.Join(",", weapons)}}{{(weapons.Length > 0 ? "," : "")}}
                { "id": "u", "name": "Squad", "type": "model",
                  "profiles": [ { "id": "pu", "name": "Squad", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ],
                  {{unitBody}} } ],
              "entryLinks": [ { "id": "rl", "targetId": "u", "type": "selectionEntry" } ] } }
            """;
        var db = Fixtures.Db(("C.json", json));
        var warnings = new List<string>();
        var unit = RosterExtractor.Extract(db, "C.json", warnings).Units.Single();
        return (unit, warnings);
    }

    [Fact]
    public void A_group_nested_in_a_group_is_mapped_on_its_own_limits()
    {
        var (u, warnings) = Extract($$"""
            "entryLinks": [ {{Link("l0", "base", Min1)}} ],
            "selectionEntryGroups": [ { "id": "support", "name": "Support Systems",
              "constraints": [ { "type": "max", "value": 2, "field": "selections", "scope": "parent" } ],
              "selectionEntryGroups": [ { "id": "secondary", "name": "Secondary Weapons",
                "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ],
                "entryLinks": [ {{Link("l1", "a")}}, {{Link("l2", "b")}} ] } ] } ]
            """, Weapon("base", "Base"), Weapon("a", "Alpha"), Weapon("b", "Beta"));
        var gear = u.UnitComposition.ModelCompositions.Single().Wargear.Single();
        Assert.Equal(["base"], gear.InitialWargear);
        var option = Assert.Single(gear.Options);
        Assert.Equal(1, option.Max);
        Assert.Equal([["alpha"], ["beta"]], option.Options.Select(c => c.Items));
        Assert.DoesNotContain(warnings, w => w.Contains("not reached"));
    }

    [Fact]
    public void A_weapon_no_option_path_reaches_becomes_an_optional_addition_and_is_reported()
    {
        // A group with a minimum but no maximum is not a shape the mapper understands.
        var (u, warnings) = Extract($$"""
            "selectionEntryGroups": [ { "id": "odd", "name": "Odd Choice",
              "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ],
              "entryLinks": [ {{Link("l1", "a")}} ] } ]
            """, Weapon("a", "Alpha"));
        var option = Assert.Single(u.UnitComposition.ModelCompositions.Single().Wargear.Single().Options);
        Assert.Equal(["alpha"], Assert.Single(option.Options).Items);
        Assert.Contains(warnings, w => w.Contains("alpha") && w.Contains("optional addition"));
    }

    [Fact]
    public void Weapon_entries_whose_names_differ_only_in_letter_case_share_one_weapon_set()
    {
        var (u, _) = Extract($$"""
            "entryLinks": [ {{Link("l1", "a", Min1)}}, {{Link("l2", "b", Min1)}} ]
            """, Weapon("a", "Shuriken Pistol"), Weapon("b", "Shuriken pistol"));
        Assert.Single(u.Weapons);                                              // two sets with one id fail the builder's validation
    }

    [Fact]
    public void A_statline_no_model_refers_to_is_still_shown()
    {
        var (u, _) = Extract("""
            "selectionEntries": [ { "id": "hound", "name": "Hounds", "type": "upgrade",
              "profiles": [ { "id": "ph", "name": "Hounds", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "12\"" } ] } ] } ]
            """);
        Assert.Equal(["Squad", "Hounds"], u.StatLines.Select(s => s.StatName));
        Assert.Equal(StatFeature.None, u.StatLines.Single(s => s.StatName == "Squad").Features);
        Assert.Equal(StatFeature.AlwaysShow, u.StatLines.Single(s => s.StatName == "Hounds").Features);
    }

    [Fact]
    public void Options_that_replace_the_same_items_with_an_overlapping_choice_are_merged()
    {
        var unit = new Unit();
        unit.UnitComposition.ModelCompositions.Add(new ModelComposition
        {
            ModelName = "Model",
            Wargear =
            [
                new ModelWargear
                {
                    InitialWargear = ["gun"],
                    Options =
                    [
                        new WargearOption { Replaces = ["gun"], Max = 1, Options = [new WargearChoice { Items = ["flamer"] }] },
                        new WargearOption { Replaces = ["gun"], Max = 2, Options = [new WargearChoice { Items = ["flamer"] }, new WargearChoice { Items = ["melta"] }] },
                    ],
                },
            ],
        });
        VariantMerger.Merge(unit, new CompositionExtractor.Result([], 0, 0, [], []), []);
        var option = Assert.Single(unit.UnitComposition.ModelCompositions[0].Wargear[0].Options);
        Assert.Equal([["flamer"], ["melta"]], option.Options.Select(c => c.Items));
        Assert.Equal(2, option.Max);
    }

    [Fact]
    public void Options_that_replace_different_items_stay_separate()
    {
        var unit = new Unit();
        unit.UnitComposition.ModelCompositions.Add(new ModelComposition
        {
            ModelName = "Model",
            Wargear =
            [
                new ModelWargear
                {
                    InitialWargear = ["gun", "sword"],
                    Options =
                    [
                        new WargearOption { Replaces = ["gun"], Options = [new WargearChoice { Items = ["flamer"] }] },
                        new WargearOption { Replaces = ["sword"], Options = [new WargearChoice { Items = ["flamer"] }] },
                    ],
                },
            ],
        });
        VariantMerger.Merge(unit, new CompositionExtractor.Result([], 0, 0, [], []), []);
        Assert.Equal(2, unit.UnitComposition.ModelCompositions[0].Wargear[0].Options.Count);
    }
}
