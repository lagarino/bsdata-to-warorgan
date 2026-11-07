using WarOrgan.Model.Unit;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>
/// Variant models of one group are merged so the group's total cap applies: without this a unit "10-20 Warriors" whose
/// two loadouts each allow 20 would let a list field 40 models for the price of 20.
/// </summary>
public class VariantMergerTests
{
    private const string Warriors = """
        { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
          { "id": "flayer", "name": "Gauss flayer", "type": "upgrade", "profiles": [ { "id": "pf", "name": "Gauss flayer", "typeName": "Ranged Weapons", "characteristics": [ { "name": "A", "$text": "1" } ] } ] },
          { "id": "reaper", "name": "Gauss reaper", "type": "upgrade", "profiles": [ { "id": "pr", "name": "Gauss reaper", "typeName": "Ranged Weapons", "characteristics": [ { "name": "A", "$text": "2" } ] } ] },
          { "id": "u", "name": "Warriors", "type": "unit",
            "profiles": [ { "id": "pu", "name": "Warriors", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "5\"" } ] } ],
            "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 90 } ],
            "selectionEntryGroups": [ { "id": "g", "name": "10-20 Warriors", "defaultSelectionEntryId": "m1",
              "constraints": [ { "type": "min", "value": 10, "field": "selections", "scope": "parent" }, { "type": "max", "value": 20, "field": "selections", "scope": "parent" } ],
              "selectionEntries": [
                { "id": "m1", "name": "Warrior w/ flayer", "type": "model", "constraints": [ { "type": "max", "value": 20, "field": "selections", "scope": "parent" } ],
                  "entryLinks": [ { "id": "l1", "targetId": "flayer", "type": "selectionEntry", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] } ] },
                { "id": "m2", "name": "Warrior w/ reaper", "type": "model", "constraints": [ { "type": "max", "value": 20, "field": "selections", "scope": "parent" } ],
                  "entryLinks": [ { "id": "l2", "targetId": "reaper", "type": "selectionEntry", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] } ] } ] } ] } ],
          "entryLinks": [ { "id": "rl", "targetId": "u", "type": "selectionEntry" } ] } }
        """;

    private static (Unit Unit, List<string> Warnings) Extract(string json)
    {
        var db = Fixtures.Db(("C.json", json));
        var resolved = new RosterResolver(db).Resolve("C.json").Single();
        var warnings = new List<string>();
        return (new UnitExtractor(db).Extract(resolved, new HashSet<string>(), warnings), warnings);
    }

    [Fact]
    public void Two_loadouts_of_one_group_become_one_model_with_the_group_limit()
    {
        var (u, _) = Extract(Warriors);
        var model = Assert.Single(u.UnitComposition.ModelCompositions);
        Assert.Equal("Warriors", model.ModelName);                          // named after the shared statline
        Assert.Equal((10, 20), (model.Limit!.Min, model.Limit.Max));        // not 0-20 for each variant
    }

    [Fact]
    public void The_other_loadout_becomes_a_replacement_option_with_no_extra_cap()
    {
        var (u, _) = Extract(Warriors);
        var gear = Assert.Single(u.UnitComposition.ModelCompositions).Wargear.Single();
        Assert.Equal(["gauss flayer"], gear.InitialWargear);
        var option = Assert.Single(gear.Options);
        Assert.Equal(["gauss flayer"], option.Replaces);
        Assert.Equal(["gauss reaper"], Assert.Single(option.Options).Items);
        Assert.Null(option.Max);                                            // bounded by the model count itself
    }

    [Fact]
    public void Both_weapons_are_still_selectable_and_listed()
    {
        var (u, warnings) = Extract(Warriors);
        Assert.Equal(["gauss flayer", "gauss reaper"], u.Weapons.SelectMany(w => w.Weapons).Select(w => w.Id!).Order());
        Assert.DoesNotContain(warnings, w => w.Contains("not part of any model"));
    }

    [Fact]
    public void Boyz_and_their_nob_are_not_merged_because_their_statlines_differ()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var resolved = new RosterResolver(db).Resolve("Orks.json").Single(r => BsdDatabase.Str(r.Entry, "name") == "Boyz");
        var u = new UnitExtractor(db).Extract(resolved, new HashSet<string>(), []);
        Assert.Equal(["Boy", "Nob"], u.UnitComposition.ModelCompositions.Select(m => m.ModelName));
        Assert.Equal([(9, 18), (1, 2)], u.UnitComposition.ModelCompositions.Select(m => (m.Limit!.Min, m.Limit.Max)));
    }

    [Fact]
    public void Variants_sharing_a_sub_group_share_its_cap_in_one_option()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var resolved = new RosterResolver(db).Resolve("Orks.json").Single(r => BsdDatabase.Str(r.Entry, "name") == "Boyz");
        var u = new UnitExtractor(db).Extract(resolved, new HashSet<string>(), []);
        var option = Assert.Single(u.UnitComposition.ModelCompositions[0].Wargear.Single().Options);
        Assert.Equal(3, option.Max);                                        // "Special Weapons" allows 3 in total
    }

    [Fact]
    public void Identical_variants_merge_and_carry_their_own_nested_choice()
    {
        const string crawlers = """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "ga", "name": "Reaper", "type": "upgrade", "profiles": [ { "id": "pa", "name": "Reaper", "typeName": "Ranged Weapons", "characteristics": [ { "name": "A", "$text": "2" } ] } ] },
              { "id": "gb", "name": "Isolator", "type": "upgrade", "profiles": [ { "id": "pb", "name": "Isolator", "typeName": "Ranged Weapons", "characteristics": [ { "name": "A", "$text": "1" } ] } ] },
              { "id": "u", "name": "Crawlers", "type": "unit",
                "profiles": [ { "id": "pu", "name": "Crawler", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "8\"" } ] } ],
                "selectionEntryGroups": [ { "id": "g", "name": "2 Crawlers", "selectionEntries": [
                  { "id": "m1", "name": "Crawler", "type": "model", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ],
                    "entryLinks": [ { "id": "a1", "targetId": "ga", "type": "selectionEntry", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] } ] },
                  { "id": "m2", "name": "Crawler", "type": "model", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ],
                    "selectionEntryGroups": [ { "id": "wc", "name": "Weapon choice", "defaultSelectionEntryId": "d1",
                      "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ],
                      "entryLinks": [ { "id": "d1", "targetId": "ga", "type": "selectionEntry" }, { "id": "d2", "targetId": "gb", "type": "selectionEntry" } ] } ] } ] } ] } ],
              "entryLinks": [ { "id": "rl", "targetId": "u", "type": "selectionEntry" } ] } }
            """;
        var (u, _) = Extract(crawlers);
        var model = Assert.Single(u.UnitComposition.ModelCompositions);
        Assert.Equal((2, 2), (model.Limit!.Min, model.Limit.Max));
        var gear = model.Wargear.Single();
        Assert.Equal(["reaper"], gear.InitialWargear);
        var option = Assert.Single(gear.Options);
        Assert.Equal(["reaper"], option.Replaces);
        Assert.Equal(["isolator"], Assert.Single(option.Options).Items);
    }
}
