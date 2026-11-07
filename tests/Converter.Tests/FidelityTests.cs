using WarOrgan.Model.Faction;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;

namespace Converter.Tests;

/// <summary>Shapes found when the strict checks were extended to more catalogues.</summary>
public class FidelityTests
{
    private const string Pts = "51b2-306e-1021-d207";

    private static string Unit(string id, string name, string body, string costs = "")
    {
        var tail = (costs + " " + body).Trim().TrimEnd(',').Trim();
        return $$"""
            { "id": "{{id}}", "name": "{{name}}", "type": "unit",
              "profiles": [ { "id": "p-{{id}}", "name": "{{name}}", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ]{{(tail.Length > 0 ? ", " + tail : "")}} }
            """;
    }

    private static string Cost(int pts) => $$""" "costs": [ { "typeId": "{{Pts}}", "name": "pts", "value": {{pts}} } ], """;

    private static (RosterResult Roster, List<string> Warnings) Run(string catalogueBody, params (string File, string Json)[] extra)
    {
        var files = new List<(string, string)> { ("C.json", $$"""{ "catalogue": { "id": "c", "name": "C", {{catalogueBody}} } }""") };
        files.AddRange(extra);
        var db = Fixtures.Db(files.ToArray());
        var warnings = new List<string>();
        return (RosterExtractor.Extract(db, "C.json", warnings), warnings);
    }

    private const string Categories = """
        "categoryEntries": [ { "id": "c-trooper", "name": "Trooper" }, { "id": "c-crusade", "name": "Crusade Force" } ],
        """;

    private static string RootLink(string unitId) => $$"""{ "id": "rl-{{unitId}}", "targetId": "{{unitId}}", "type": "selectionEntry" }""";

    // ---- points ----

    [Fact]
    public void A_price_on_the_model_entries_is_a_per_model_cost()
    {
        var unit = Unit("u", "Squad", """
            "selectionEntryGroups": [ { "id": "g", "name": "2-4 Squad",
              "constraints": [ { "type": "min", "value": 2, "field": "selections", "scope": "parent" }, { "type": "max", "value": 4, "field": "selections", "scope": "parent" } ],
              "selectionEntries": [ { "id": "m", "name": "Trooper", "type": "model", "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 30 } ] } ] } ]
            """);
        var (r, _) = Run($$"""{{Categories}} "sharedSelectionEntries": [ {{unit}} ], "entryLinks": [ {{RootLink("u")}} ]""");
        Assert.Equal([(2, 60), (3, 90), (4, 120)], r.Units.Single().Points.Select(p => (p.ModelCount, p.Cost)));
    }

    private static List<WarOrgan.Model.Unit.PointsDefinition> PricedUnit(string modifiers, out List<string> warnings)
    {
        var unit = Unit("u", "Squad", $$"""
            "categoryLinks": [ { "targetId": "c-trooper" } ],
            "modifiers": [ {{modifiers}} ],
            "selectionEntryGroups": [ { "id": "g", "name": "2-5 Squad",
              "constraints": [ { "type": "min", "value": 2, "field": "selections", "scope": "parent" }, { "type": "max", "value": 5, "field": "selections", "scope": "parent" } ],
              "selectionEntries": [ { "id": "m", "name": "Trooper", "type": "model" } ] } ]
            """, Cost(50));
        var (r, w) = Run($$"""{{Categories}} "sharedSelectionEntries": [ {{unit}} ], "entryLinks": [ {{RootLink("u")}} ]""");
        warnings = w;
        return r.Units.Single().Points;
    }

    private static string Mod(string type, int value, string conditions) =>
        $$"""{ "field": "51b2-306e-1021-d207", "type": "{{type}}", "value": {{value}}, "conditions": [ {{conditions}} ] }""";

    private static string Cond(string type, string child, string scope, int value) =>
        $$"""{ "type": "{{type}}", "childId": "{{child}}", "field": "selections", "scope": "{{scope}}", "value": {{value}} }""";

    [Fact]
    public void Entries_of_campaign_modes_are_never_active()
    {
        var points = PricedUnit(string.Join(",",
            Mod("set", 99, Cond("instanceOf", "c-crusade", "force", 1)),          // only in a campaign force: ignored
            Mod("increment", 5, Cond("notInstanceOf", "c-crusade", "force", 1))),  // "not in a campaign force": applies
            out var warnings);
        Assert.All(points, p => Assert.Equal(55, p.Cost));
        Assert.Empty(warnings.Where(w => w.Contains("points")));
    }

    [Fact]
    public void The_count_of_the_only_model_type_is_the_models_in_the_unit()
    {
        var points = PricedUnit(Mod("set", 120, Cond("atLeast", "c-trooper", "u", 4)), out var warnings);
        Assert.Equal([(3, 50), (5, 120)], points.Select(p => (p.ModelCount, p.Cost)));   // from 4 models on
        Assert.DoesNotContain(warnings, w => w.Contains("unsupported"));
    }

    [Fact]
    public void An_unknown_condition_next_to_a_known_false_one_is_not_reported()
    {
        var points = PricedUnit(Mod("set", 1, Cond("atLeast", "some-upgrade", "self", 1) + "," + Cond("instanceOf", "c-crusade", "force", 1)), out var warnings);
        Assert.All(points, p => Assert.Equal(50, p.Cost));
        Assert.DoesNotContain(warnings, w => w.Contains("unsupported"));
    }

    [Fact]
    public void An_unknown_condition_that_decides_the_outcome_is_reported()
    {
        var points = PricedUnit(Mod("set", 1, Cond("atLeast", "some-upgrade", "self", 1)), out var warnings);
        Assert.All(points, p => Assert.Equal(50, p.Cost));
        Assert.Contains(warnings, w => w.Contains("unsupported"));
    }

    // ---- model counts ----

    [Fact]
    public void A_group_without_a_size_whose_default_is_a_model_next_to_it_substitutes_for_that_model()
    {
        const string body = """
            "selectionEntries": [ { "id": "m-base", "name": "Trooper", "type": "model",
              "constraints": [ { "type": "min", "value": 9, "field": "selections", "scope": "parent" }, { "type": "max", "value": 9, "field": "selections", "scope": "parent" } ] } ],
            "selectionEntryGroups": [ { "id": "g-opt", "name": "Options", "defaultSelectionEntryId": "m-base",
              "selectionEntries": [
                { "id": "m-a", "name": "Trooper w/ A", "type": "model", "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] },
                { "id": "m-b", "name": "Trooper w/ B", "type": "model", "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] } ] } ]
            """;
        var db = Fixtures.Db(("C.json", $$"""{ "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [ {{Unit("u", "Squad", body)}} ] } }"""));
        var r = new CompositionExtractor(db).Extract(Fixtures.Entry(db, "u"), []);
        Assert.Equal((9, 9), (r.MinModels, r.MaxModels));                          // the variants are among the 9, not on top
        Assert.Equal(3, r.Models.Count);
    }

    [Fact]
    public void An_outer_cap_and_a_nested_minimum_make_the_unit_size()
    {
        const string body = """
            "selectionEntryGroups": [ { "id": "outer", "name": "Squad",
              "constraints": [ { "type": "max", "value": 9, "field": "selections", "scope": "parent" } ],
              "selectionEntries": [ { "id": "m-one", "name": "Champion", "type": "model", "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] } ],
              "selectionEntryGroups": [ { "id": "inner", "name": "Troopers",
                "constraints": [ { "type": "min", "value": 4, "field": "selections", "scope": "parent" } ],
                "selectionEntries": [ { "id": "m-t", "name": "Trooper", "type": "model", "constraints": [ { "type": "max", "value": 9, "field": "selections", "scope": "parent" } ] } ] } ] } ]
            """;
        var db = Fixtures.Db(("C.json", $$"""{ "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [ {{Unit("u", "Squad", body)}} ] } }"""));
        var r = new CompositionExtractor(db).Extract(Fixtures.Entry(db, "u"), []);
        Assert.Equal((4, 9), (r.MinModels, r.MaxModels));
    }

    // ---- detachments and enhancements ----

    private static string Detachments(string id, string name) => $$"""
        { "id": "{{id}}", "name": "Detachment", "type": "upgrade", "selectionEntries": [
          { "id": "d-{{id}}", "name": "{{name}}", "type": "upgrade",
            "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "DP", "value": 1 } ],
            "rules": [ { "id": "r-{{id}}", "name": "Rule {{name}}", "description": "text" } ] } ] }
        """;

    [Fact]
    public void Detachments_come_from_a_library_the_roster_s_own_units_come_from_but_not_from_an_allied_library()
    {
        const string own = """{ "catalogue": { "id": "lib-own", "name": "Own Library", "library": true, "sharedSelectionEntries": [ OWNUNIT, DETACH ], "entryLinks": [] } }""";
        var ownUnit = Unit("u-own", "Own Squad", "");
        var alliedUnit = Unit("u-ally", "Allied Squad", "");
        var ownLib = own.Replace("OWNUNIT", ownUnit).Replace("DETACH", Detachments("det-own", "Own Detachment"));
        var alliedLib = $$"""{ "catalogue": { "id": "lib-ally", "name": "Allied Library", "library": true, "sharedSelectionEntries": [ {{alliedUnit}}, {{Detachments("det-ally", "Allied Detachment")}} ] } }""";
        var body = $$"""
            "catalogueLinks": [ { "targetId": "lib-own", "importRootEntries": true }, { "targetId": "lib-ally", "importRootEntries": true } ],
            "entryLinks": [ {{RootLink("u-own")}} ]
            """;
        // The own library's unit is linked from this catalogue's root (its own unit); the allied library's unit is only imported.
        var (r, _) = Run(body, ("OwnLib.json", ownLib), ("AlliedLib.json", alliedLib));
        Assert.Equal(["Own Detachment"], r.Detachments.Select(d => d.Name));
    }

    [Fact]
    public void A_library_detachment_hidden_by_the_primary_catalogue_is_dropped()
    {
        const string hideForC = """
            "modifiers": [ { "field": "hidden", "type": "set", "value": true, "conditions": [ { "type": "instanceOf", "childId": "c", "field": "selections", "scope": "primary-catalogue", "value": 1 } ] } ],
            """;
        var detach = $$"""
            { "id": "Detachment", "name": "Detachment", "type": "upgrade", "selectionEntries": [
              { "id": "d-shown", "name": "Shown", "type": "upgrade", "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "DP", "value": 1 } ], "rules": [ { "id": "r1", "name": "R1", "description": "t" } ] },
              { "id": "d-hidden", "name": "Hidden", "type": "upgrade", {{hideForC}} "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "DP", "value": 1 } ], "rules": [ { "id": "r2", "name": "R2", "description": "t" } ] } ] }
            """;
        var lib = $$"""{ "catalogue": { "id": "lib", "name": "Shared Library", "library": true, "sharedSelectionEntries": [ {{Unit("u-lib", "Lib Squad", "")}}, {{detach}} ] } }""";
        const string body = """
            "catalogueLinks": [ { "targetId": "lib", "importRootEntries": true } ],
            "entryLinks": [ { "id": "rl", "targetId": "u-lib", "type": "selectionEntry" } ]
            """;
        var (r, _) = Run(body, ("Lib.json", lib));
        Assert.Equal(["Shown"], r.Detachments.Select(d => d.Name));
    }

    private static string EnhancementCatalogue(string enhancementText, string hide) => $$"""
        "sharedSelectionEntries": [
          {{Unit("u-boyz", "Boyz", "")}},
          {{Detachments("Detachment", "Alpha")}} ],
        "sharedSelectionEntryGroups": [ { "id": "g", "name": "Enhancements", "selectionEntries": [
          { "id": "e1", "name": "Trick", "type": "upgrade", "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 10 } ],
            "profiles": [ { "id": "pe", "name": "Trick", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "{{enhancementText}}" } ] } ],
            "modifiers": [ { "field": "hidden", "type": "set", "value": true, "conditionGroups": [ { "type": "and", "conditions": [ {{hide}} ] } ] } ] } ] } ],
        "entryLinks": [ {{RootLink("u-boyz")}} ]
        """;

    private const string NotSelectedAsEqualZero =
        """{ "type": "equalTo", "childId": "d-Detachment", "field": "selections", "scope": "force", "value": 0 }""";

    [Fact]
    public void A_detachment_that_is_hidden_when_its_count_equals_zero_owns_the_enhancement()
    {
        var (r, _) = Run(EnhancementCatalogue("Does a thing.", NotSelectedAsEqualZero));
        Assert.Equal(["Trick"], r.Detachments.Single().Enhancements.Select(e => e.Name));
    }

    [Fact]
    public void A_unit_only_text_names_the_unit_that_may_take_the_enhancement()
    {
        var (r, _) = Run(EnhancementCatalogue("**BOYZ** unit only. Does a thing.", NotSelectedAsEqualZero));
        Assert.Equal(["BOYZ"], r.Detachments.Single().Enhancements.Single().RequiredUnitNames);
    }

    [Fact]
    public void An_enhancement_for_a_unit_outside_the_roster_is_left_out_and_reported()
    {
        var (r, warnings) = Run(EnhancementCatalogue("**GHOSTS** unit only. Does a thing.", NotSelectedAsEqualZero));
        Assert.Empty(r.Detachments.Single().Enhancements);
        Assert.Contains(warnings, w => w.Contains("Trick") && w.Contains("not in this roster"));
    }
}
