using System.Text.Json.Nodes;
using WarOrganConverter.Load;

namespace Converter.Tests;

/// <summary>
/// Synthetic BSData fixtures. Names and numbers are invented; no real game content lives in this repo (decision D5).
/// The JSON shapes mirror what the real catalogues look like (ids, constraints, modifiers, links).
/// </summary>
public static class Fixtures
{
    public const string Pts = "51b2-306e-1021-d207";

    /// <summary>Writes the given catalogues to a temp folder and loads them.</summary>
    public static BsdDatabase Db(params (string File, string Json)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "bsdata-to-warorgan-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (file, json) in files) File.WriteAllText(Path.Combine(dir, file), json);
        return BsdDatabase.Load(dir);
    }

    public static JsonObject Entry(BsdDatabase db, string id) => db.ById[id].Node;

    public const string GameSystem = """
    { "gameSystem": { "id": "gs", "name": "Test Game", "type": "gameSystem",
      "sharedRules": [
        { "id": "r-dd", "name": "Deadly Demise", "description": "Boom." },
        { "id": "r-fnp", "name": "Feel No Pain", "description": "Ignore wounds." },
        { "id": "r-leader", "name": "Leader", "description": "Attach." }
      ] } }
    """;

    /// <summary>A small catalogue with one of each shape the extractors handle.</summary>
    public const string Orks = """
    { "catalogue": { "id": "cat-orks", "name": "Test - Orks", "type": "catalogue",
      "categoryEntries": [
        { "id": "c-inf", "name": "Infantry" }, { "id": "c-char", "name": "Character" },
        { "id": "c-fac", "name": "Faction: Orks" }, { "id": "c-boyz", "name": "Boyz" }
      ],
      "sharedRules": [
        { "id": "r-waaagh", "name": "Waaagh!", "description": "Army rule." },
        { "id": "r-da-boss", "name": "Da Boss", "description": "Unit rule." }
      ],
      "sharedSelectionEntries": [
        { "id": "u-boyz", "name": "Boyz", "type": "unit", "hidden": false,
          "categoryLinks": [ { "targetId": "c-inf" }, { "targetId": "c-boyz" }, { "targetId": "c-fac" } ],
          "infoLinks": [
            { "type": "rule", "targetId": "r-waaagh", "name": "Waaagh!" },
            { "type": "rule", "targetId": "r-dd", "name": "Deadly Demise", "modifiers": [ { "field": "name", "type": "append", "value": "D3" } ] }
          ],
          "profiles": [ { "id": "p-ab", "name": "Ammo Runts", "typeName": "Abilities", "characteristics": [ { "name": "Description", "$text": "Plus one to hit." } ] } ],
          "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 85 } ],
          "modifiers": [
            { "field": "51b2-306e-1021-d207", "type": "set", "value": 170,
              "conditions": [ { "type": "greaterThan", "field": "selections", "childId": "model", "scope": "u-boyz", "value": 10 } ] },
            { "field": "51b2-306e-1021-d207", "type": "increment", "value": 10,
              "conditionGroups": [ { "type": "and", "localConditionGroups": [
                { "type": "atLeast", "value": 3, "field": "selections", "scope": "parent",
                  "conditions": [ { "type": "before", "childId": "any", "field": "selections", "scope": "self", "value": 1 },
                                  { "type": "instanceOf", "childId": "u-boyz", "field": "selections", "scope": "self", "value": 1 } ] } ] } ] }
          ],
          "selectionEntryGroups": [
            { "id": "g-boyz", "name": "9-18 Boyz",
              "constraints": [ { "type": "min", "value": 9, "field": "selections", "scope": "parent" }, { "type": "max", "value": 18, "field": "selections", "scope": "parent" } ],
              "selectionEntries": [
                { "id": "m-boy", "name": "Boy", "type": "model",
                  "constraints": [ { "type": "max", "value": 18, "field": "selections", "scope": "parent" } ],
                  "profiles": [ { "id": "p-boy", "name": "Boy", "typeName": "Unit", "characteristics": [
                    { "name": "M", "$text": "6\"" }, { "name": "T", "$text": "5" }, { "name": "Sv", "$text": "5+" }, { "name": "InSv", "$text": "" }, { "name": "Base", "$text": "32mm" } ] } ],
                  "entryLinks": [ { "id": "l-choppa", "targetId": "w-choppa", "type": "selectionEntry",
                                    "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] } ] },
                { "id": "g-special", "name": "Special Weapons",
                  "constraints": [ { "type": "max", "value": 3, "field": "selections", "scope": "parent" } ],
                  "selectionEntries": [
                    { "id": "m-boy-bs", "name": "Boy w/ Big shoota", "type": "model",
                      "constraints": [ { "type": "max", "value": 2, "field": "selections", "scope": "parent" } ],
                      "profiles": [ { "id": "p-boy2", "name": "Boy", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ],
                      "entryLinks": [ { "id": "l-bs", "targetId": "w-bigshoota", "type": "selectionEntry",
                                        "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] } ] } ] } ] },
            { "id": "g-nobz", "name": "1-2 Nobz",
              "selectionEntries": [
                { "id": "m-nob", "name": "Nob", "type": "model",
                  "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 2, "field": "selections", "scope": "parent" } ],
                  "profiles": [ { "id": "p-nob", "name": "Nob", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" }, { "name": "W", "$text": "3" } ] } ],
                  "selectionEntryGroups": [
                    { "id": "g-melee", "name": "Melee",
                      "defaultSelectionEntryId": "l-nob-choppa",
                      "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" }, { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ],
                      "entryLinks": [ { "id": "l-nob-choppa", "targetId": "w-choppa", "type": "selectionEntry" },
                                      { "id": "l-nob-klaw", "targetId": "w-klaw", "type": "selectionEntry" } ] } ],
                  "selectionEntries": [
                    { "id": "up-twin", "name": "Twin Killsaw", "type": "upgrade",
                      "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ],
                      "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 5 } ],
                      "profiles": [ { "id": "p-tk", "name": "Twin Killsaw", "typeName": "Melee Weapons", "characteristics": [ { "name": "Range", "$text": "Melee" }, { "name": "A", "$text": "4" }, { "name": "Keywords", "$text": "TWIN-LINKED" } ] } ] },
                    { "id": "up-crusade", "name": "Crusade Relics", "type": "upgrade",
                      "profiles": [ { "id": "p-cr", "name": "Relic Blade", "typeName": "Melee Weapons", "characteristics": [ { "name": "Range", "$text": "Melee" }, { "name": "A", "$text": "9" } ] } ] } ] } ] }
          ] },
        { "id": "u-warboss", "name": "Warboss", "type": "model", "hidden": false,
          "categoryLinks": [ { "targetId": "c-char" }, { "targetId": "c-fac" } ],
          "infoLinks": [ { "type": "rule", "targetId": "r-leader", "name": "Leader" }, { "type": "rule", "targetId": "r-da-boss", "name": "Da Boss" } ],
          "costs": [ { "typeId": "51b2-306e-1021-d207", "name": "pts", "value": 100 } ],
          "profiles": [ { "id": "p-wb", "name": "Warboss", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" }, { "name": "W", "$text": "6" } ] } ],
          "selectionEntryGroups": [ { "id": "g-wg", "name": "Wargear",
            "entryLinks": [ { "id": "l-wb-choppa", "targetId": "w-choppa", "type": "selectionEntry",
                              "constraints": [ { "type": "min", "value": 1, "field": "selections", "scope": "parent" } ] },
                            { "id": "l-wb-bs", "targetId": "w-bigshoota", "type": "selectionEntry",
                              "constraints": [ { "type": "max", "value": 1, "field": "selections", "scope": "parent" } ] } ] } ] },
        { "id": "u-legend", "name": "Old Boss [Legends]", "type": "model",
          "profiles": [ { "id": "p-lg", "name": "Old Boss", "typeName": "Unit", "characteristics": [ { "name": "M", "$text": "6\"" } ] } ] },
        { "id": "u-unlinked", "name": "Never Linked", "type": "unit" },
        { "id": "w-choppa", "name": "Choppa", "type": "upgrade",
          "profiles": [ { "id": "p-choppa", "name": "Choppa", "typeName": "Melee Weapons", "characteristics": [ { "name": "Range", "$text": "Melee" }, { "name": "A", "$text": "3" }, { "name": "WS", "$text": "3+" }, { "name": "Keywords", "$text": "" } ] } ] },
        { "id": "w-klaw", "name": "Power Klaw", "type": "upgrade",
          "profiles": [ { "id": "p-klaw", "name": "Power Klaw", "typeName": "Melee Weapons", "characteristics": [ { "name": "Range", "$text": "Melee" }, { "name": "A", "$text": "3" } ] } ] },
        { "id": "w-bigshoota", "name": "Big Shoota ", "type": "upgrade",
          "profiles": [
            { "id": "p-bs1", "name": "Big Shoota - Aimed", "typeName": "Ranged Weapons", "characteristics": [ { "name": "Range", "$text": "36\"" }, { "name": "A", "$text": "3" }, { "name": "BS", "$text": "5+" }, { "name": "Keywords", "$text": "RAPID FIRE 2" } ] },
            { "id": "p-bs2", "name": "Big Shoota - Burst", "typeName": "Ranged Weapons", "characteristics": [ { "name": "Range", "$text": "24\"" }, { "name": "A", "$text": "6" }, { "name": "BS", "$text": "5+" }, { "name": "Keywords", "$text": "" } ] } ] }
      ],
      "entryLinks": [
        { "id": "rl-boyz", "targetId": "u-boyz", "type": "selectionEntry" },
        { "id": "rl-warboss", "targetId": "u-warboss", "type": "selectionEntry" },
        { "id": "rl-legend", "targetId": "u-legend", "type": "selectionEntry" },
        { "id": "rl-choppa", "targetId": "w-choppa", "type": "selectionEntry" }
      ] } }
    """;
}
