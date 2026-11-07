using WarOrganConverter.Extract;
using WarOrganConverter.Load;

namespace Converter.Tests;

/// <summary>Phase 3, stage 5: model compositions and the points tiers.</summary>
public class CompositionAndPointsTests
{
    private static BsdDatabase OrksDb() => Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));

    private static CompositionExtractor.Result Compose(BsdDatabase db, string id, List<string>? warnings = null) =>
        new CompositionExtractor(db).Extract(Fixtures.Entry(db, id), warnings ?? []);

    [Fact]
    public void A_single_model_in_a_group_takes_the_groups_limits()
    {
        var r = Compose(OrksDb(), "u-boyz");
        var boy = r.Models.Single(m => m.ModelName == "Boy");
        Assert.Equal((9, 18), (boy.Limit!.Min, boy.Limit.Max));         // group 9-18 intersected with the model's own max 18
        Assert.Equal("Boy", boy.StatlineRef);
    }

    [Fact]
    public void Variant_models_in_a_nested_group_are_counted_inside_the_parent_group()
    {
        var r = Compose(OrksDb(), "u-boyz");
        var bigShoota = r.Models.Single(m => m.ModelName == "Boy w/ Big shoota");
        Assert.Equal((0, 2), (bigShoota.Limit!.Min, bigShoota.Limit.Max));
        Assert.Equal(["Boy", "Boy w/ Big shoota", "Nob"], r.Models.Select(m => m.ModelName));
        Assert.Equal((10, 20), (r.MinModels, r.MaxModels));              // 9-18 Boyz plus 1-2 Nobz; variants add nothing
    }

    [Fact]
    public void A_top_level_model_is_a_single_model_datasheet()
    {
        var r = Compose(OrksDb(), "u-warboss");
        var m = Assert.Single(r.Models);
        Assert.Equal("Warboss", m.ModelName);
        Assert.Equal((1, 1), (m.Limit!.Min, m.Limit.Max));
        Assert.Equal("Warboss", m.StatlineRef);
        Assert.Equal((1, 1), (r.MinModels, r.MaxModels));
    }

    [Fact]
    public void Groups_without_models_of_their_own_are_descended_into()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "u", "name": "Destroyers", "type": "unit",
                "selectionEntryGroups": [ { "id": "outer", "name": "Unit Composition",
                  "selectionEntryGroups": [ { "id": "inner", "name": "3-6 Destroyers",
                    "constraints": [ { "type": "min", "value": 3, "field": "selections", "scope": "parent" }, { "type": "max", "value": 6, "field": "selections", "scope": "parent" } ],
                    "selectionEntries": [ { "id": "m", "name": "Destroyer", "type": "model",
                      "profiles": [ { "id": "p", "name": "Destroyer", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "8\"" } ] } ] } ] } ] } ] } ] } }
            """));
        var r = Compose(db, "u");
        Assert.Equal((3, 6), (r.Models.Single().Limit!.Min, r.Models.Single().Limit!.Max));
        Assert.Equal((3, 6), (r.MinModels, r.MaxModels));
    }

    [Fact]
    public void Variant_models_without_a_profile_use_the_unit_level_profile()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "u", "name": "Warriors", "type": "unit",
                "profiles": [ { "id": "p", "name": "Warriors", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "5\"" } ] } ],
                "selectionEntryGroups": [ { "id": "g", "name": "10-20 Warriors",
                  "constraints": [ { "type": "min", "value": 10, "field": "selections", "scope": "parent" }, { "type": "max", "value": 20, "field": "selections", "scope": "parent" } ],
                  "selectionEntries": [ { "id": "m1", "name": "Warrior w/ flayer", "type": "model" }, { "id": "m2", "name": "Warrior w/ reaper", "type": "model" } ] } ] } ] } }
            """));
        var r = Compose(db, "u");
        Assert.All(r.Models, m => Assert.Equal("Warriors", m.StatlineRef));
    }

    [Fact]
    public void Models_with_the_same_name_are_renamed_so_names_stay_unique()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "u", "name": "Crawlers", "type": "unit",
                "profiles": [ { "id": "p", "name": "Crawler", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "8\"" } ] } ],
                "selectionEntryGroups": [ { "id": "g", "name": "2 Crawlers",
                  "selectionEntries": [
                    { "id": "m1", "name": "Crawler", "type": "model", "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] },
                    { "id": "m2", "name": "Crawler", "type": "model", "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] } ] } ] } ] } }
            """));
        var warnings = new List<string>();
        var r = Compose(db, "u", warnings);
        Assert.Equal(["Crawler", "Crawler (2)"], r.Models.Select(m => m.ModelName));
        Assert.Contains(warnings, w => w.Contains("appears 2 times"));
    }

    [Fact]
    public void A_unit_without_models_is_reported()
    {
        var db = Fixtures.Db(("C.json", """{ "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [ { "id": "u", "name": "Empty", "type": "unit" } ] } }"""));
        var warnings = new List<string>();
        Compose(db, "u", warnings);
        Assert.Contains(warnings, w => w.Contains("no model entries"));
    }

    // ---- points ----

    [Fact]
    public void Points_are_ceiling_tiers_over_model_count_and_earlier_copies()
    {
        var r = new PointsExtractor().Extract(Fixtures.Entry(OrksDb(), "u-boyz"), "Boyz", 10, 20);
        Assert.Empty(r.Unsupported);
        // 85 for up to 10 models, 170 above; from the fourth copy on (3 earlier copies) everything costs 10 more.
        Assert.Equal(
            [(3, 10, 85), (3, 20, 170), (4, 10, 95), (4, 20, 180)],
            r.Points.Select(p => (p.UnitCount!.Value, p.ModelCount, p.Cost)));
    }

    [Fact]
    public void A_flat_cost_is_a_single_tier_without_unit_count()
    {
        var r = new PointsExtractor().Extract(Fixtures.Entry(OrksDb(), "u-warboss"), "Warboss", 1, 1);
        var p = Assert.Single(r.Points);
        Assert.Equal((1, 100, (int?)null), (p.ModelCount, p.Cost, p.UnitCount));
    }

    [Fact]
    public void Conditions_the_evaluator_does_not_understand_are_reported_and_not_applied()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "u", "name": "Odd", "type": "unit",
                "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 50 } ],
                "modifiers": [ { "field": "51b2-306e-1021-d207", "type": "increment", "value": 5,
                  "conditions": [ { "type": "atLeast", "field": "selections", "childId": "some-upgrade", "scope": "self", "value": 1 } ] } ] } ] } }
            """));
        var r = new PointsExtractor().Extract(Fixtures.Entry(db, "u"), "Odd", 1, 1);
        Assert.Equal(50, Assert.Single(r.Points).Cost);
        Assert.Contains(r.Unsupported, s => s.Contains("atLeast selections"));
    }

    [Fact]
    public void Model_count_conditions_in_force_scope_are_not_misread_as_the_unit()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "u", "name": "Scoped", "type": "unit",
                "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 50 } ],
                "modifiers": [ { "field": "51b2-306e-1021-d207", "type": "set", "value": 99,
                  "conditions": [ { "type": "atLeast", "field": "selections", "childId": "model", "scope": "force", "value": 1 } ] } ] } ] } }
            """));
        var r = new PointsExtractor().Extract(Fixtures.Entry(db, "u"), "Scoped", 1, 1);
        Assert.Equal(50, Assert.Single(r.Points).Cost);
        Assert.NotEmpty(r.Unsupported);
    }
}
