using WarOrgan.Model.Unit;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>Phase 3, stage 5: statlines, weapons, keywords and abilities of a unit.</summary>
public class ExtractionTests
{
    private static (Unit Unit, List<string> Warnings) Extract(string name, params string[] armyRules)
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var resolved = new RosterResolver(db).Resolve("Orks.json").Single(u => BsdDatabase.Str(u.Entry, "name") == name);
        var warnings = new List<string>();
        return (new UnitExtractor(db).Extract(resolved, armyRules.ToHashSet(), warnings), warnings);
    }

    [Fact]
    public void Unit_identity()
    {
        var (u, _) = Extract("Boyz");
        Assert.Equal("BOYZ", u.Id);
        Assert.Equal("Boyz", u.DisplayName);
    }

    [Fact]
    public void Statlines_come_from_unit_profiles_of_every_model_once()
    {
        var (u, _) = Extract("Boyz");
        Assert.Equal(["Boy", "Nob"], u.StatLines.Select(s => s.StatName));   // the second "Boy" profile is deduplicated
    }

    [Fact]
    public void Empty_characteristics_and_base_size_are_not_statline_values()
    {
        var (u, _) = Extract("Boyz");
        Assert.Equal(["M", "T", "Sv"], u.StatLines.Single(s => s.StatName == "Boy").Stats.Select(c => c.Id));
        Assert.Equal("6\"", u.StatLines[0].Stats[0].Value);
    }

    [Fact]
    public void Weapons_are_grouped_by_the_entry_that_owns_the_profiles()
    {
        var (u, _) = Extract("Boyz");
        Assert.Equal(["Big Shoota", "Choppa", "Power Klaw", "Twin Killsaw"], u.Weapons.Select(w => w.DisplayName).Order());
        Assert.Equal(["BIG SHOOTA", "CHOPPA", "POWER KLAW", "TWIN KILLSAW"], u.Weapons.Select(w => w.Id).Order());
    }

    [Fact]
    public void Firing_modes_share_one_weapon_id_and_keywords_are_split_from_stats()
    {
        var (u, _) = Extract("Boyz");
        var bigShoota = u.Weapons.Single(w => w.DisplayName == "Big Shoota");
        Assert.Equal(["big shoota", "big shoota"], bigShoota.Weapons.Select(w => w.Id));
        Assert.Equal(["Big Shoota - Aimed", "Big Shoota - Burst"], bigShoota.Weapons.Select(w => w.DisplayName));
        Assert.Equal("RAPID FIRE 2", bigShoota.Weapons[0].Keywords);
        Assert.Null(bigShoota.Weapons[1].Keywords);                       // empty keywords become null
        Assert.DoesNotContain(bigShoota.Weapons[0].Stats, c => c.Id == "Keywords");
        Assert.Equal(["Range", "A", "BS"], bigShoota.Weapons[0].Stats.Select(c => c.Id));
    }

    [Fact]
    public void Weapons_inside_crusade_subtrees_are_ignored()
    {
        var (u, _) = Extract("Boyz");
        Assert.DoesNotContain(u.Weapons, w => w.DisplayName == "Relic Blade");
    }

    [Fact]
    public void Faction_categories_become_faction_keywords_and_the_rest_unit_keywords()
    {
        var (u, _) = Extract("Boyz");
        Assert.Equal(["Infantry", "Boyz"], u.Keywords);
        Assert.Equal(["Orks"], u.FactionKeywords);
    }

    [Fact]
    public void Game_system_rules_are_core_abilities_with_the_unconditional_appended_value()
    {
        var (u, _) = Extract("Boyz", "r-waaagh");
        Assert.Equal(["Deadly Demise D3"], u.CoreAbilities);
    }

    [Fact]
    public void Army_rules_are_left_out_of_unit_abilities()
    {
        var (withArmyRule, _) = Extract("Warboss", "r-da-boss");
        Assert.Empty(withArmyRule.UnitAbilities);
        var (without, _) = Extract("Warboss");
        Assert.Equal(["Da Boss"], without.UnitAbilities.Select(a => a.Title));
        Assert.Equal("Unit rule.", without.UnitAbilities[0].Text);
    }

    [Fact]
    public void Leader_is_not_a_core_ability_and_ability_profiles_become_unit_abilities()
    {
        var (warboss, _) = Extract("Warboss");
        Assert.Empty(warboss.CoreAbilities);
        var (boyz, _) = Extract("Boyz", "r-waaagh");
        Assert.Equal("Plus one to hit.", boyz.UnitAbilities.Single(a => a.Title == "Ammo Runts").Text);
    }

    [Fact]
    public void Army_rules_are_rules_linked_by_most_units()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var roster = new RosterResolver(db).Resolve("Orks.json");
        var boyz = roster.Single(u => BsdDatabase.Str(u.Entry, "name") == "Boyz");
        var warboss = roster.Single(u => BsdDatabase.Str(u.Entry, "name") == "Warboss");
        var many = new[] { boyz, warboss, warboss, warboss, warboss };       // 1 of 5 link Waaagh!, 4 of 5 link Da Boss
        var armyRules = UnitExtractor.FindArmyRules(many);
        Assert.Contains("r-da-boss", armyRules);
        Assert.DoesNotContain("r-waaagh", armyRules);
    }

    [Fact]
    public void A_unit_without_a_unit_profile_is_reported()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("Orks.json", Fixtures.Orks));
        var entry = Fixtures.Entry(db, "u-unlinked");
        var warnings = new List<string>();
        new UnitExtractor(db).Extract(new ResolvedUnit(entry, "Orks.json", false), new HashSet<string>(), warnings);
        Assert.Contains(warnings, w => w.Contains("no unit profile"));
    }
}

/// <summary>An ability granted only while a leader is attached (the link is hidden when no association exists) is not a baseline ability.</summary>
public class ConditionalAbilityTests
{
    private const string Catalogue = """
    { "catalogue": { "id": "cat-x", "name": "Test - X", "type": "catalogue",
      "sharedSelectionEntries": [
        { "id": "u-x", "name": "Guards", "type": "unit", "hidden": false,
          "infoLinks": [
            { "type": "rule", "targetId": "r-fnp", "name": "Feel No Pain",
              "modifiers": [ { "field": "name", "type": "append", "value": "5+" },
                             { "field": "hidden", "type": "set", "value": true, "conditions": [ { "type": "lessThan", "field": "associations", "scope": "self", "childId": "x", "value": 1 } ] } ] },
            { "type": "rule", "targetId": "r-dd", "name": "Deadly Demise",
              "modifiers": [ { "field": "hidden", "type": "set", "value": true, "conditions": [ { "type": "atLeast", "field": "selections", "scope": "self", "childId": "x", "value": 1 } ] } ] }
          ] }
      ],
      "entryLinks": [ { "id": "l-x", "name": "Guards", "type": "selectionEntry", "targetId": "u-x" } ] } }
    """;

    [Fact]
    public void Links_hidden_until_something_is_attached_are_left_out_but_links_hidden_by_a_selection_stay()
    {
        var db = Fixtures.Db(("Game.json", Fixtures.GameSystem), ("X.json", Catalogue));
        var resolved = new RosterResolver(db).Resolve("X.json").Single();
        var unit = new UnitExtractor(db).Extract(resolved, new HashSet<string>(), []);
        Assert.DoesNotContain(unit.CoreAbilities, a => a.StartsWith("Feel No Pain"));
        Assert.Contains("Deadly Demise", unit.CoreAbilities);
    }
}
