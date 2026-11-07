using WarOrgan.Model.Effects;
using WarOrgan.Model.Faction;
using WarOrganConverter.Extract;

namespace Converter.Tests;

/// <summary>What a selected detachment changes on a datasheet.</summary>
public class DetachmentEffectTests
{
    private const string GameSystem = """
        { "gameSystem": { "id": "gs", "name": "Test Game", "type": "gameSystem",
          "profileTypes": [
            { "id": "pt-r", "name": "Ranged Weapons", "characteristicTypes": [ { "id": "ch-rs", "name": "S" }, { "id": "ch-rk", "name": "Keywords" } ] },
            { "id": "pt-m", "name": "Melee Weapons", "characteristicTypes": [ { "id": "ch-ms", "name": "S" }, { "id": "ch-mk", "name": "Keywords" } ] },
            { "id": "pt-u", "name": "Unit", "characteristicTypes": [ { "id": "ch-um", "name": "M" } ] } ] } }
        """;

    private static string AtLeast(string detachment) =>
        $$"""{ "type": "atLeast", "childId": "{{detachment}}", "field": "selections", "scope": "force", "value": 1 }""";

    private static string Mod(string field, string type, string value, string condition) =>
        $$"""{ "field": "{{field}}", "type": "{{type}}", "value": {{value}}, "conditions": [ {{condition}} ] }""";

    private static string Catalogue(string unitModifiers, string unitModifierGroups = "") => $$"""
        { "catalogue": { "id": "c", "name": "Test - Orks",
          "categoryEntries": [ { "id": "c-bl", "name": "Battleline" }, { "id": "c-fac", "name": "Faction: Orks" } ],
          "sharedProfiles": [ { "id": "ab1", "name": "Bonus", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "Bonus text." } ] } ],
          "sharedSelectionEntries": [
            { "id": "Detachment", "name": "Detachment", "type": "upgrade", "selectionEntries": [
              { "id": "d-a", "name": "Alpha", "type": "upgrade", "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "DP", "value": 1 } ], "rules": [ { "id": "ra", "name": "A", "description": "a" } ] },
              { "id": "d-b", "name": "Beta", "type": "upgrade", "costs": [ { "typeId": "82ae-1066-5107-6ae0", "name": "DP", "value": 1 } ], "rules": [ { "id": "rb", "name": "B", "description": "b" } ] } ] },
            { "id": "u", "name": "Rig", "type": "model", "categoryLinks": [ { "targetId": "c-fac" } ],
              "profiles": [ { "id": "pu", "name": "Rig", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "8\"" } ] } ],
              "selectionEntries": [
                { "id": "gun", "name": "Gun", "type": "upgrade", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ],
                  "profiles": [ { "id": "pg", "name": "Gun", "typeName": "Ranged Weapons", "characteristics": [ { "name": "S", "$text": "4" }, { "name": "Keywords", "$text": "" } ] } ] },
                { "id": "blade", "name": "Blade", "type": "upgrade", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ],
                  "profiles": [ { "id": "pb", "name": "Blade", "typeName": "Melee Weapons", "characteristics": [ { "name": "S", "$text": "5" }, { "name": "Keywords", "$text": "" } ] } ] },
                { "id": "staff", "name": "Staff", "type": "upgrade", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ],
                  "profiles": [ { "id": "ps1", "name": "Staff", "typeName": "Ranged Weapons", "characteristics": [ { "name": "S", "$text": "5" } ] },
                                { "id": "ps2", "name": "Staff", "typeName": "Melee Weapons", "characteristics": [ { "name": "S", "$text": "5" } ] } ] } ],
              "modifiers": [ {{unitModifiers}} ],
              "modifierGroups": [ {{unitModifierGroups}} ] }
          ],
          "entryLinks": [ { "id": "rl", "targetId": "u", "type": "selectionEntry" } ] } }
        """;

    private static (RosterResult Roster, List<string> Warnings) Run(string catalogue)
    {
        var db = Fixtures.Db(("Game.json", GameSystem), ("C.json", catalogue));
        var warnings = new List<string>();
        return (RosterExtractor.Extract(db, "C.json", warnings), warnings);
    }

    private static DetachmentEffect Effect(RosterResult r, string detachment) =>
        Assert.Single(r.Detachments.Single(d => d.Name == detachment).Effects);

    private static string ModifierGroup(string detachment, string field, string type, string value) => $$"""
        { "conditions": [ {{AtLeast(detachment)}} ],
          "modifiers": [ { "field": "{{field}}", "type": "{{type}}", "value": {{value}} } ] }
        """;

    private static readonly string AlphaMods = string.Join(",",
        Mod("ch-ms", "increment", "1", AtLeast("d-a")),                          // melee strength +1
        Mod("ch-mk", "append", "\"SUSTAINED HITS 1\"", AtLeast("d-a")),          // melee keyword
        Mod("ch-um", "increment", "6", AtLeast("d-a")),                          // unit movement +6
        Mod("add-info", "add", "\"ab1\"", AtLeast("d-a")),                       // added ability profile
        Mod("category", "add", "\"c-bl\"", AtLeast("d-a")),                      // added category
        Mod("annotation", "append", "\"Alpha\"", AtLeast("d-a")));               // display only

    [Fact]
    public void Weapon_changes_apply_to_the_weapons_of_that_profile_type_only()
    {
        var (r, _) = Run(Catalogue(AlphaMods));
        var effect = Effect(r, "Alpha");
        Assert.Equal(["RIG"], effect.AffectedUnits);
        var strength = effect.WeaponStatAdjustments!.Single(a => a.TargetStatId == "S");
        Assert.Equal((StatAdjustmentType.Add, "1"), (strength.Type, strength.Value));
        Assert.Contains("blade", strength.NameFilter!);
        Assert.DoesNotContain("gun", strength.NameFilter!);                      // ranged weapons are not melee weapons
    }

    [Fact]
    public void A_keyword_append_becomes_an_add_keywords_adjustment()
    {
        var (r, _) = Run(Catalogue(AlphaMods));
        var kw = Effect(r, "Alpha").WeaponStatAdjustments!.Single(a => a.TargetStatId == "Keywords");
        Assert.Equal((StatAdjustmentType.Add, "SUSTAINED HITS 1"), (kw.Type, kw.Value));
        Assert.Contains("blade", kw.NameFilter!);
    }

    [Fact]
    public void Unit_characteristics_added_abilities_and_categories_are_converted()
    {
        var (r, _) = Run(Catalogue(AlphaMods));
        var effect = Effect(r, "Alpha");
        var move = Assert.Single(effect.StatAdjustments!);
        Assert.Equal(("M", StatAdjustmentType.Add, "6"), (move.TargetStatId, move.Type, move.Value));
        Assert.Equal(("Bonus", "Bonus text."), (effect.AddedAbilities!.Single().Title, effect.AddedAbilities!.Single().Text));
        var keywords = Assert.Single(effect.DatasheetAdjustments!);
        Assert.Equal(DatasheetFeature.Keywords, keywords.Target);
        Assert.Equal(["Battleline"], keywords.Values);
    }

    [Fact]
    public void A_weapon_with_a_ranged_and_a_melee_profile_gets_the_same_change_once()
    {
        var both = string.Join(",", Mod("ch-rs", "increment", "2", AtLeast("d-a")), Mod("ch-ms", "increment", "2", AtLeast("d-a")));
        var (r, warnings) = Run(Catalogue(both));
        var s = Effect(r, "Alpha").WeaponStatAdjustments!.Single(a => a.TargetStatId == "S");   // one adjustment, not two (that would add 4)
        Assert.Contains("staff", s.NameFilter!);
        Assert.Contains("gun", s.NameFilter!);
        Assert.Contains("blade", s.NameFilter!);
        Assert.DoesNotContain(warnings, w => w.Contains("different"));
    }

    [Fact]
    public void Different_changes_to_one_weapons_two_profiles_are_reported()
    {
        var clash = string.Join(",", Mod("ch-rs", "increment", "1", AtLeast("d-a")), Mod("ch-ms", "increment", "2", AtLeast("d-a")));
        var (_, warnings) = Run(Catalogue(clash));
        Assert.Contains(warnings, w => w.Contains("staff") && w.Contains("different"));
    }

    [Fact]
    public void A_modifier_inside_a_group_whose_condition_names_the_detachment_is_found()
    {
        var group = ModifierGroup("d-b", "ch-rs", "increment", "3");
        var (r, _) = Run(Catalogue("", group));
        var s = Effect(r, "Beta").WeaponStatAdjustments!.Single();
        Assert.Equal("3", s.Value);
        Assert.Contains("gun", s.NameFilter!);
        Assert.Empty(r.Detachments.Single(d => d.Name == "Alpha").Effects);       // only the named detachment
    }

    [Fact]
    public void Modifiers_with_other_conditions_are_reported_not_applied()
    {
        var gated = $$"""
            { "field": "ch-ms", "type": "increment", "value": 1, "conditions": [ {{AtLeast("d-a")}},
              { "type": "atLeast", "childId": "something-else", "field": "selections", "scope": "force", "value": 1 } ] }
            """;
        var (r, warnings) = Run(Catalogue(gated));
        Assert.Empty(r.Detachments.Single(d => d.Name == "Alpha").Effects);
        Assert.Contains(warnings, w => w.Contains("not converted") && w.Contains("Rig"));
    }

    [Fact]
    public void Display_annotations_are_ignored_without_a_warning()
    {
        var (_, warnings) = Run(Catalogue(Mod("annotation", "append", "\"Alpha\"", AtLeast("d-a"))));
        Assert.DoesNotContain(warnings, w => w.Contains("annotation"));
    }

    [Fact]
    public void Units_a_detachment_does_not_touch_have_no_effect_entries()
    {
        var (r, _) = Run(Catalogue(Mod("ch-ms", "increment", "1", AtLeast("d-a"))));
        Assert.Empty(r.Detachments.Single(d => d.Name == "Beta").Effects);
    }
}
