using WarOrgan.Model.Unit;
using WarOrganConverter.Extract;

namespace Converter.Tests;

/// <summary>Leader, support and retainer attachments built from "associations".</summary>
public class LeaderTests
{
    private static string Cond(string category) =>
        $$"""{ "type": "instanceOf", "childId": "{{category}}", "field": "selections", "scope": "self", "shared": true, "value": 1 }""";

    private static string Assoc(string label, string conditionGroups) =>
        $$"""{ "name": "x", "label": "{{label}}", "action": "group", "min": 0, "max": 1, "conditionGroups": [ {{conditionGroups}} ] }""";

    private static string Or(params string[] conditions) => $$"""{ "type": "or", "conditions": [ {{string.Join(",", conditions)}} ] }""";

    private static string Entry(string id, string name, string type, string[] categories, string? association = null) => $$"""
        { "id": "{{id}}", "name": "{{name}}", "type": "{{type}}",
          "categoryLinks": [ {{string.Join(",", categories.Select(c => $$"""{ "targetId": "{{c}}" }"""))}} ]
          {{(association is null ? "" : $$""", "associations": [ {{association}} ]""")}} }
        """;

    private static (List<Unit> Units, List<string> Warnings) Extract(params string[] entries)
    {
        var links = string.Join(",", entries.Select((_, i) => $$"""{ "id": "rl{{i}}", "targetId": "u{{i}}", "type": "selectionEntry" }"""));
        var json = $$"""
            { "catalogue": { "id": "c", "name": "C",
              "categoryEntries": [ { "id": "c-a", "name": "Alpha" }, { "id": "c-b", "name": "Beta" }, { "id": "c-inf", "name": "Infantry" },
                                   { "id": "c-tek", "name": "Cryptek" }, { "id": "c-g", "name": "Gamma" } ],
              "sharedSelectionEntries": [ {{string.Join(",", entries)}} ],
              "entryLinks": [ {{links}} ] } }
            """;
        var db = Fixtures.Db(("C.json", json));
        var warnings = new List<string>();
        return (RosterExtractor.Extract(db, "C.json", warnings).Units, warnings);
    }

    private static LeaderInfo? Info(List<Unit> units, string name) => units.Single(u => u.DisplayName == name).LeaderInfo;

    private static readonly string[] Bodyguards =
    [
        Entry("u0", "Alpha Squad", "unit", ["c-a", "c-inf"]),
        Entry("u1", "Beta Squad", "unit", ["c-b", "c-inf"]),
    ];

    // Entry ids are positional ("u0", "u1", ...): the two bodyguards come first, so leaders must be called u2, u3, ...
    private static (List<Unit> Units, List<string> Warnings) WithLeaders(params string[] leaders) =>
        Extract(Bodyguards.Concat(leaders).ToArray());

    [Fact]
    public void A_leader_naming_units_by_their_own_category_lists_those_units()
    {
        var (units, _) = WithLeaders(Entry("u2", "Boss", "model", ["c-g"], Assoc("Leader", Or(Cond("c-a"), Cond("c-b")))));
        var info = Info(units, "Boss")!;
        Assert.Equal(["ALPHA SQUAD", "BETA SQUAD"], info.UnitNames);
        Assert.Null(info.LeaderType);                                  // default leader type
        Assert.Null(info.AttachableByKeyword);
    }

    [Fact]
    public void A_category_shared_by_several_units_becomes_an_attachable_keyword()
    {
        var (units, _) = WithLeaders(Entry("u2", "Captain", "model", ["c-g"], Assoc("Leader", Or(Cond("c-inf")))));
        var info = Info(units, "Captain")!;
        Assert.Empty(info.UnitNames);
        Assert.Equal(["Infantry"], info.AttachableByKeyword);
    }

    [Fact]
    public void Supported_by_makes_a_support_attachment()
    {
        var (units, _) = WithLeaders(Entry("u2", "Medic", "model", ["c-g"], Assoc("Supported by", Or(Cond("c-a")))));
        var info = Info(units, "Medic")!;
        Assert.Equal(LeaderInfo.SupportLeaderId, info.LeaderType);
        Assert.Equal(["ALPHA SQUAD"], info.UnitNames);
    }

    [Fact]
    public void A_unit_that_can_only_be_joined_with_a_selected_option_is_left_out_and_reported()
    {
        const string gated = """
            { "name": "x", "label": "Leader", "action": "group", "min": 0, "max": 1, "conditionGroups": [ { "type": "or", "conditions": [
                { "type": "instanceOf", "childId": "c-a", "field": "selections", "scope": "self", "value": 1 } ],
              "conditionGroups": [ { "type": "and", "conditions": [
                { "type": "atLeast", "childId": "up-hat", "childName": "Fancy Hat", "field": "selections", "scope": "self", "value": 1 },
                { "type": "instanceOf", "childId": "c-b", "field": "selections", "scope": "self", "value": 1 } ] } ] } ] }
            """;
        var (units, warnings) = WithLeaders(Entry("u2", "Hatted Boss", "model", ["c-g"], gated));
        Assert.Equal(["ALPHA SQUAD"], Info(units, "Hatted Boss")!.UnitNames);
        Assert.Contains(warnings, w => w.Contains("Hatted Boss") && w.Contains("Beta Squad") && w.Contains("only with"));
    }

    [Fact]
    public void Retainers_need_a_leader_with_the_keyword_and_may_join_the_units_those_leaders_join()
    {
        var (units, _) = WithLeaders(
            Entry("u2", "Tek One", "model", ["c-tek"], Assoc("Leader", Or(Cond("c-a")))),
            Entry("u3", "Tek Two", "model", ["c-tek"], Assoc("Leader", Or(Cond("c-b")))),
            Entry("u4", "Thralls", "unit", ["c-g"], Assoc("Retainers", Or(Cond("c-tek")))));
        var info = Info(units, "Thralls")!;
        Assert.Equal("Retainers", info.LeaderType);
        Assert.Equal(["Cryptek"], info.RequiredLeaderKeywords);
        Assert.Equal(["ALPHA SQUAD", "BETA SQUAD"], info.UnitNames.Order());
    }

    [Fact]
    public void An_attachment_that_names_nothing_in_the_roster_is_dropped_with_a_warning()
    {
        var (units, warnings) = WithLeaders(Entry("u2", "Lost", "model", ["c-g"], Assoc("Leader", Or(Cond("c-missing")))));
        Assert.Null(Info(units, "Lost"));
        Assert.Contains(warnings, w => w.Contains("Lost") && w.Contains("no unit of this roster"));
    }

    [Fact]
    public void Units_without_associations_get_no_leader_info()
    {
        var (units, _) = WithLeaders();
        Assert.All(units, u => Assert.Null(u.LeaderInfo));
    }

    [Fact]
    public void A_leader_never_lists_itself()
    {
        var (units, _) = WithLeaders(Entry("u2", "Boss", "model", ["c-a"], Assoc("Leader", Or(Cond("c-a")))));
        Assert.DoesNotContain("BOSS", Info(units, "Boss")!.UnitNames);
    }
}
