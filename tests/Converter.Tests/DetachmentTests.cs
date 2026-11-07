using WarOrgan.Model.Faction;
using WarOrgan.Model.Unit;
using WarOrganConverter.Extract;

namespace Converter.Tests;

/// <summary>Detachments, their rules, force dispositions, enhancements and who may take them.</summary>
public class DetachmentTests
{
    private const string Hide = """
        { "field": "hidden", "type": "set", "value": true, "conditionGroups": [ { "type": "and", "conditions": [
            { "type": "lessThan", "childId": "{0}", "field": "selections", "scope": "force", "value": 1 } ] } ] }
        """;

    private static string HideUnless(string detachmentId) => Hide.Replace("{0}", detachmentId);

    private static readonly string Catalogue = $$"""
        { "catalogue": { "id": "c", "name": "Test - Orks",
          "categoryEntries": [
            { "id": "c-tah", "name": "Take and Hold" }, { "id": "c-ptf", "name": "Purge the Foe" }, { "id": "c-3dp", "name": "3DP Detachment" },
            { "id": "c-char", "name": "Character" }, { "id": "c-fac", "name": "Faction: Orks" }, { "id": "c-nobz", "name": "Nobz" },
            { "id": "c-boyz", "name": "Boyz" }, { "id": "c-inf", "name": "Infantry" } ],
          "sharedRules": [ { "id": "r-beta", "name": "Beta Rule", "description": "Beta text." } ],
          "sharedSelectionEntries": [
            { "id": "ba", "name": "Boarding Actions", "type": "upgrade" },
            { "id": "u-nobz", "name": "Nobz", "type": "unit", "categoryLinks": [ { "targetId": "c-nobz" }, { "targetId": "c-inf" }, { "targetId": "c-fac" } ],
              "profiles": [ { "id": "p1", "name": "Nob", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ],
              "selectionEntries": [ { "id": "m1", "name": "Nob", "type": "model" } ] },
            { "id": "u-boyz", "name": "Boyz", "type": "unit", "categoryLinks": [ { "targetId": "c-boyz" }, { "targetId": "c-inf" }, { "targetId": "c-fac" } ],
              "profiles": [ { "id": "p2", "name": "Boy", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ],
              "selectionEntryGroups": [ { "id": "g-emb", "name": "Enhancements", "modifiers": [ {{HideUnless("d-b")}} ],
                "selectionEntries": [ { "id": "e-emb", "name": "Boyz Trick", "type": "upgrade", "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 5 } ],
                  "profiles": [ { "id": "pe", "name": "Boyz Trick", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "A trick." } ] } ] } ] } ] },
            { "id": "u-boss", "name": "Boss", "type": "model", "categoryLinks": [ { "targetId": "c-char" }, { "targetId": "c-fac" } ],
              "profiles": [ { "id": "p3", "name": "Boss", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ],
              "entryLinks": [ { "id": "lk", "targetId": "g-enh", "type": "selectionEntryGroup" } ] },
            { "id": "u-grunt", "name": "Grunt Chief", "type": "model", "categoryLinks": [ { "targetId": "c-char" }, { "targetId": "c-fac" } ],
              "profiles": [ { "id": "p4", "name": "Grunt Chief", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ] },
            { "id": "Detachment", "name": "Detachment", "type": "upgrade",
              "selectionEntries": [
                { "id": "d-a", "name": "Alpha Force", "type": "upgrade",
                  "categoryLinks": [ { "targetId": "c-tah" }, { "targetId": "c-3dp" }, { "targetId": "c-ptf" } ],
                  "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "Detachment Points", "value": 3 } ],
                  "rules": [ { "id": "ra", "name": "Alpha Rule", "description": "Alpha text." } ] },
                { "id": "d-b", "name": "Beta Force", "type": "upgrade",
                  "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "Detachment Points", "value": 1 } ],
                  "infoLinks": [ { "type": "rule", "targetId": "r-beta", "name": "Beta Rule" } ] },
                { "id": "d-board", "name": "Boarding Squad", "type": "upgrade",
                  "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "Detachment Points", "value": 1 } ],
                  "modifiers": [ { "field": "hidden", "type": "set", "value": true, "conditions": [ { "type": "notInstanceOf", "childId": "ba", "field": "selections", "scope": "force", "value": 1 } ] } ],
                  "rules": [ { "id": "rb", "name": "Boarding Rule", "description": "Not for us." } ] } ] }
          ],
          "sharedSelectionEntryGroups": [
            { "id": "g-enh", "name": "Enhancements",
              "selectionEntryGroups": [ { "id": "g-alpha", "name": "Alpha Force Enhancements", "modifiers": [ {{HideUnless("d-a")}} ],
                "selectionEntries": [
                  { "id": "e-hat", "name": "Big Hat", "type": "upgrade", "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 20 } ],
                    "profiles": [ { "id": "ph", "name": "Big Hat", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "**ORKS** model only. Does stuff." } ] } ],
                    "modifiers": [ { "field": "hidden", "type": "set", "value": true, "conditionGroups": [ { "type": "or",
                      "conditions": [ { "type": "notInstanceOf", "childId": "c-char", "field": "selections", "scope": "ancestor", "value": 1 },
                                      { "type": "instanceOf", "childId": "c-nobz", "field": "selections", "scope": "ancestor", "value": 1 } ] } ] } ] } ] } ],
              "selectionEntries": [ { "id": "e-lost", "name": "Orphan", "type": "upgrade",
                "profiles": [ { "id": "po", "name": "Orphan", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "Nobody shows me." } ] } ] } ] },
            { "id": "g-up", "name": "Enhancements - Upgrades",
              "selectionEntryGroups": [ { "id": "g-beta", "name": "Beta Force Enhancements", "modifiers": [ {{HideUnless("d-b")}} ],
                "selectionEntries": [ { "id": "e-nob", "name": "Nob Boost", "type": "upgrade", "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 15 } ],
                  "profiles": [ { "id": "pn", "name": "Nob Boost", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "NOBZ unit only. Faster." } ] } ],
                  "modifiers": [ { "field": "hidden", "type": "set", "value": true, "conditions": [ { "type": "notInstanceOf", "childId": "c-nobz", "field": "selections", "scope": "root-entry", "value": 1 } ] } ] } ] } ] }
          ],
          "entryLinks": [
            { "id": "r1", "targetId": "u-nobz", "type": "selectionEntry" }, { "id": "r2", "targetId": "u-boyz", "type": "selectionEntry" },
            { "id": "r3", "targetId": "u-boss", "type": "selectionEntry" }, { "id": "r4", "targetId": "u-grunt", "type": "selectionEntry" } ] } }
        """;

    private static (RosterResult Roster, List<string> Warnings) Run()
    {
        var db = Fixtures.Db(("C.json", Catalogue));
        var warnings = new List<string>();
        return (RosterExtractor.Extract(db, "C.json", warnings), warnings);
    }

    private static Detachment Det(RosterResult r, string name) => r.Detachments.Single(d => d.Name == name);

    [Fact]
    public void Detachments_come_from_the_Detachment_entry_and_boarding_only_ones_are_dropped()
    {
        var (r, _) = Run();
        Assert.Equal(["Alpha Force", "Beta Force"], r.Detachments.Select(d => d.Name));
    }

    [Fact]
    public void Cost_dispositions_and_rule_text()
    {
        var (r, _) = Run();
        var alpha = Det(r, "Alpha Force");
        Assert.Equal(3, alpha.Cost);
        Assert.Equal(["Take and Hold", "Purge the Foe"], alpha.ForceDispositions);   // "3DP Detachment" is a marker, not a disposition
        Assert.Null(alpha.Tags);                                                      // dispositions never become exclusion tags
        Assert.Equal(("Alpha Rule", "Alpha text."), (alpha.Rule!.Title, alpha.Rule.Text));
        var beta = Det(r, "Beta Force");
        Assert.Null(beta.ForceDispositions);
        Assert.Equal(("Beta Rule", "Beta text."), (beta.Rule!.Title, beta.Rule.Text)); // rule linked from a shared rule
    }

    [Fact]
    public void An_enhancement_belongs_to_the_detachment_its_visibility_condition_names()
    {
        var (r, _) = Run();
        var hat = Assert.Single(Det(r, "Alpha Force").Enhancements);
        Assert.Equal(("Big Hat", 20), (hat.Name, hat.Cost));
        Assert.StartsWith("**ORKS** model only", hat.Description);
        Assert.Equal(EnhancementFeature.None, hat.Features);
    }

    [Fact]
    public void Eligibility_is_read_from_the_negated_hide_conditions_and_from_the_model_only_text()
    {
        var (r, _) = Run();
        var hat = Det(r, "Alpha Force").Enhancements.Single();
        Assert.Equal(["Character", "Orks"], hat.RequiredKeywords.Order());   // hidden unless Character ("notInstanceOf"), plus "ORKS model only"
        Assert.Equal(["Nobz"], hat.ExcludedKeywords);                       // hidden when "instanceOf Nobz"
    }

    [Fact]
    public void An_upgrade_enhancement_is_flagged_and_tied_to_the_unit_its_category_names()
    {
        var (r, _) = Run();
        var boost = Det(r, "Beta Force").Enhancements.Single(e => e.Name == "Nob Boost");
        Assert.Equal(EnhancementFeature.Upgrade, boost.Features);
        Assert.Equal(["NOBZ"], boost.RequiredUnitNames);
    }

    [Fact]
    public void An_enhancement_inside_a_datasheet_is_an_upgrade_for_that_unit()
    {
        var (r, _) = Run();
        var trick = Det(r, "Beta Force").Enhancements.Single(e => e.Name == "Boyz Trick");
        Assert.Equal(EnhancementFeature.Upgrade, trick.Features);
        Assert.Equal(["BOYZ"], trick.RequiredUnitNames);
        Assert.Equal(5, trick.Cost);
    }

    [Fact]
    public void An_enhancement_that_no_detachment_shows_is_left_out_and_reported()
    {
        var (r, warnings) = Run();
        Assert.DoesNotContain(r.Detachments.SelectMany(d => d.Enhancements), e => e.Name == "Orphan");
        Assert.Contains(warnings, w => w.Contains("Orphan") && w.Contains("no detachment"));
    }

    [Fact]
    public void A_character_whose_datasheet_links_no_enhancement_group_cannot_take_one()
    {
        var (r, _) = Run();
        Flags(r, "Grunt Chief", expected: UnitFlags.NoEnhancements);
        Flags(r, "Boss", expected: UnitFlags.None);                         // links the Enhancements group
        Flags(r, "Nobz", expected: UnitFlags.None);                         // not a Character: the app decides by keyword
    }

    private static void Flags(RosterResult r, string name, UnitFlags expected) =>
        Assert.Equal(expected, r.Units.Single(u => u.DisplayName == name).Flags);

    [Fact]
    public void Dispositions_in_the_data_never_leak_into_unit_keywords_as_tags()
    {
        var (r, _) = Run();
        Assert.All(r.Detachments, d => Assert.Null(d.Tags));
    }
}
