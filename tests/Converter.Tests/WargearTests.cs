using WarOrgan.Model.Unit;
using WarOrgan.Model.Unit.Wargear;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>Phase 3, stage 5: starting wargear, replacement options and wargear definitions.</summary>
public class WargearTests
{
    private static Unit ExtractFrom(BsdDatabase db, string file, string? name = null)
    {
        var roster = new RosterResolver(db).Resolve(file);
        var resolved = name is null ? roster.Single() : roster.Single(u => BsdDatabase.Str(u.Entry, "name") == name);
        return new UnitExtractor(db).Extract(resolved, new HashSet<string>(), []);
    }

    private static Unit Orks(string name) => ExtractFrom(Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks)), "Orks.json", name);

    private static ModelWargear Gear(Unit u, string model) => u.UnitComposition.ModelCompositions.Single(m => m.ModelName == model).Wargear.Single();

    [Fact]
    public void Required_upgrades_are_the_models_initial_wargear()
    {
        var u = Orks("Boyz");
        Assert.Equal(["choppa"], Gear(u, "Boy").InitialWargear);
        Assert.Equal(["big shoota"], Gear(u, "Boy w/ Big shoota").InitialWargear);   // trimmed, lower-cased entry name = weapon id
    }

    [Fact]
    public void An_exactly_one_group_is_a_default_plus_a_replacement_option()
    {
        var nob = Gear(Orks("Boyz"), "Nob");
        Assert.Contains("choppa", nob.InitialWargear);                           // default found through the link id
        var option = Assert.Single(nob.Options);
        Assert.Equal(["choppa"], option.Replaces);
        Assert.Equal(["power klaw"], Assert.Single(option.Options).Items);
    }

    [Fact]
    public void An_upgrade_with_a_points_cost_becomes_a_wargear_definition_and_stays_selectable()
    {
        var u = Orks("Boyz");
        var def = Assert.Single(u.UnitComposition.WargearDefinitions);
        Assert.Equal(("twin killsaw", 5), (def.Key, def.Cost));
        Assert.Contains("twin killsaw", Gear(u, "Nob").InitialWargear);
    }

    [Fact]
    public void Crusade_entries_do_not_become_wargear()
    {
        Assert.DoesNotContain(Gear(Orks("Boyz"), "Nob").InitialWargear, k => k.Contains("relic"));
    }

    [Fact]
    public void A_plain_container_group_is_transparent_and_an_optional_upgrade_is_an_option()
    {
        var gear = Gear(Orks("Warboss"), "Warboss");
        Assert.Equal(["choppa"], gear.InitialWargear);
        var option = Assert.Single(gear.Options);
        Assert.Null(option.Replaces);
        Assert.Equal(1, option.Max);
        Assert.Equal(["big shoota"], Assert.Single(option.Options).Items);
    }

    [Fact]
    public void Every_weapon_is_selectable_so_the_builder_will_not_reject_it()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        foreach (var name in new[] { "Boyz", "Warboss" })
        {
            var resolved = new RosterResolver(db).Resolve("Orks.json").Single(u => BsdDatabase.Str(u.Entry, "name") == name);
            var warnings = new List<string>();
            var u = new UnitExtractor(db).Extract(resolved, new HashSet<string>(), warnings);
            Assert.DoesNotContain(warnings, w => w.Contains("not part of any model"));
            var known = u.UnitComposition.ModelCompositions.SelectMany(m => m.Wargear)
                .SelectMany(w => w.InitialWargear.Concat(w.Options.SelectMany(o => o.Options).SelectMany(c => c.Items)))
                .ToHashSet();
            foreach (var id in u.Weapons.SelectMany(w => w.Weapons).Select(w => w.Id!))
                Assert.Contains(id, known);
        }
    }

    [Fact]
    public void A_wrapper_choice_flattens_to_its_required_parts_with_nested_options()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "wa", "name": "Blade", "type": "upgrade", "profiles": [ { "id": "pa", "name": "Blade", "typeName": "Melee Weapons", "characteristics": [ { "name": "A", "$text": "3" } ] } ] },
              { "id": "wb", "name": "Arrow", "type": "upgrade", "profiles": [ { "id": "pb", "name": "Arrow", "typeName": "Ranged Weapons", "characteristics": [ { "name": "A", "$text": "1" } ] } ] },
              { "id": "u", "name": "Lord", "type": "model",
                "profiles": [ { "id": "pu", "name": "Lord", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "5\"" } ] } ],
                "selectionEntryGroups": [ { "id": "g", "name": "Weapons", "defaultSelectionEntryId": "wrap",
                  "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ],
                  "selectionEntries": [
                    { "id": "wrap", "name": "Blade and arrow", "type": "upgrade",
                      "entryLinks": [
                        { "id": "l1", "targetId": "wa", "type": "selectionEntry", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] },
                        { "id": "l2", "targetId": "wb", "type": "selectionEntry", "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] } ] },
                    { "id": "scythe", "name": "Scythe", "type": "upgrade",
                      "profiles": [ { "id": "ps", "name": "Scythe", "typeName": "Melee Weapons", "characteristics": [ { "name": "A", "$text": "4" } ] } ] } ] } ] } ],
              "entryLinks": [ { "id": "rl", "targetId": "u", "type": "selectionEntry" } ] } }
            """));
        var gear = ExtractFrom(db, "C.json").UnitComposition.ModelCompositions.Single().Wargear.Single();
        Assert.Equal(["blade", "arrow"], gear.InitialWargear);
        var option = Assert.Single(gear.Options);
        Assert.Equal(["blade", "arrow"], option.Replaces);                   // the alternative replaces the whole wrapper
        Assert.Equal(["scythe"], Assert.Single(option.Options).Items);
    }

    [Fact]
    public void An_upgrade_that_grants_an_ability_registers_it_as_a_unit_ability()
    {
        var db = Fixtures.Db(("C.json", """
            { "catalogue": { "id": "c", "name": "C", "sharedSelectionEntries": [
              { "id": "u", "name": "Rider", "type": "model",
                "profiles": [ { "id": "pu", "name": "Rider", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "9\"" } ] } ],
                "selectionEntries": [ { "id": "banner", "name": "Banner", "type": "upgrade",
                  "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ],
                  "profiles": [ { "id": "pb", "name": "Rally Banner", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "Rally!" } ] } ] } ] } ],
              "entryLinks": [ { "id": "rl", "targetId": "u", "type": "selectionEntry" } ] } }
            """));
        var unit = ExtractFrom(db, "C.json");
        var def = Assert.Single(unit.UnitComposition.WargearDefinitions);
        Assert.Equal(["Rally Banner"], def.GrantsAbilities);
        Assert.Contains(unit.UnitAbilities, a => a.Title == "Rally Banner" && a.Text == "Rally!");
        Assert.Contains("banner", unit.UnitComposition.ModelCompositions.Single().Wargear.Single().InitialWargear);
    }
}
