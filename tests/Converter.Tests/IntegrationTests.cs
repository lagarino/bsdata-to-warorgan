using WarOrgan.Model.Unit;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace Converter.Tests;

/// <summary>
/// Runs only when BSDATA_SRC points at a local checkout of a BSData repository; otherwise the tests are skipped.
/// They hold the checks that were first done by hand on the real data: structural invariants that should keep
/// holding when the BSData repository is updated. They do not pin exact counts, which drift with the data.
/// </summary>
public class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrEmpty(IntegrationTests.Source))
            Skip = "Set BSDATA_SRC to a BSData checkout to run the integration tests.";
    }
}

public class IntegrationTests
{
    public static string? Source => Environment.GetEnvironmentVariable("BSDATA_SRC");

    private static readonly Lazy<BsdDatabase> Database = new(() => BsdDatabase.Load(Source!));

    /// <summary>Catalogues checked by hand in the app.</summary>
    public static IEnumerable<object[]> Factions =>
        [["Orks.json"], ["Necrons.json"]];

    /// <summary>Every catalogue that converts and passes the builder's validation: all invariants below hold for each of them.</summary>
    public static IEnumerable<object[]> AllFactions =>
        Factions.Concat([["T'au Empire.json"], ["Leagues of Votann.json"], ["Imperium - Grey Knights.json"],
         ["Imperium - Adepta Sororitas.json"], ["Aeldari - Craftworlds.json"], ["Aeldari - Drukhari.json"], ["Tyranids.json"],
         ["Imperium - Adeptus Custodes.json"], ["Imperium - Adeptus Mechanicus.json"]]);

    private static (List<Unit> Units, List<string> Warnings) ExtractAll(string file)
    {
        var warnings = new List<string>();
        return (RosterExtractor.Extract(Database.Value, file, warnings).Units, warnings);
    }

    [IntegrationFact]
    public void Every_catalogue_loads_and_resolves_a_roster()
    {
        var db = Database.Value;
        var resolver = new RosterResolver(db);
        foreach (var (file, root) in db.Roots.Where(kv => BsdDatabase.Str(kv.Value, "type") != "gameSystem" && !Scope.IsExcludedFile(kv.Key)))
            Assert.NotNull(resolver.Resolve(file));
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Roster_has_a_plausible_size_and_no_out_of_scope_entries(string file)
    {
        var db = Database.Value;
        var roster = new RosterResolver(db).Resolve(file);
        Assert.InRange(roster.Count, 20, 200);
        Assert.DoesNotContain(roster, u => Scope.IsExcludedUnitName(BsdDatabase.Str(u.Entry, "name")));
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Every_unit_has_statlines_models_and_points(string file)
    {
        var (units, _) = ExtractAll(file);
        foreach (var u in units)
        {
            Assert.True(u.StatLines.Count > 0, $"{u.Id}: no statline");
            Assert.True(u.UnitComposition.ModelCompositions.Count > 0, $"{u.Id}: no model composition");
            Assert.True(u.Points.Count > 0, $"{u.Id}: no points");
            Assert.All(u.Points, p => Assert.True(p.Cost >= 0 && p.ModelCount > 0, $"{u.Id}: bad tier"));
            Assert.Equal(u.StatLines.Count, u.StatLines.Select(s => s.StatName).Distinct().Count());
        }
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Every_model_refers_to_an_existing_statline_and_unique_name(string file)
    {
        var (units, _) = ExtractAll(file);
        foreach (var u in units)
        {
            var models = u.UnitComposition.ModelCompositions;
            var dupes = models.GroupBy(m => m.ModelName).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(dupes.Count == 0, $"{u.Id}: duplicate model names: {string.Join(", ", dupes)}");
            foreach (var m in models)
                Assert.Contains(u.StatLines, s => s.StatName == m.StatlineRef);
        }
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void The_model_limits_together_never_allow_more_models_than_the_points_table_prices(string file)
    {
        // Regression: variant models used to carry 0-N limits each, so a unit could field a multiple of its real maximum.
        var (units, _) = ExtractAll(file);
        foreach (var u in units)
        {
            var allowed = u.UnitComposition.ModelCompositions.Where(m => m.UpgradeCost == null).Sum(m => m.Limit!.Max);
            var priced = u.Points.Max(p => p.ModelCount);
            Assert.True(allowed <= priced, $"{u.Id}: models may total {allowed} but points only price up to {priced}");
        }
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Leader_attachments_name_real_units_and_every_character_leads_something(string file)
    {
        var (units, _) = ExtractAll(file);
        var ids = units.Select(u => u.Id).ToHashSet();
        foreach (var u in units.Where(u => u.LeaderInfo is not null))
        {
            Assert.All(u.LeaderInfo!.UnitNames, n => Assert.Contains(n, ids));
            Assert.True(u.LeaderInfo.UnitNames.Count > 0 || u.LeaderInfo.AttachableByKeyword is { Count: > 0 }, $"{u.Id}: empty leader info");
        }
        Assert.Contains(units, u => u.LeaderInfo is not null);
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Detachments_have_costs_rules_and_well_formed_enhancements(string file)
    {
        var warnings = new List<string>();
        var roster = RosterExtractor.Extract(Database.Value, file, warnings);
        Assert.InRange(roster.Detachments.Count, 3, 40);
        Assert.Equal(roster.Detachments.Count, roster.Detachments.Select(d => d.Name).Distinct().Count());
        foreach (var d in roster.Detachments)
        {
            Assert.True(d.Cost >= 1, $"{d.Name}: cost");
            Assert.NotNull(d.Rule);
            Assert.All(d.Enhancements, e =>
            {
                Assert.False(string.IsNullOrWhiteSpace(e.Description), $"{d.Name}/{e.Name}: no description");
                Assert.True(e.Cost >= 0, $"{d.Name}/{e.Name}: cost");
                if (e.Features.HasFlag(WarOrgan.Model.Faction.EnhancementFeature.Upgrade))
                    Assert.True(e.RequiredUnitNames.Count + e.RequiredKeywords.Count + e.RequiredOneOfKeywords.Count + e.ExcludedKeywords.Count > 0,
                        $"{d.Name}/{e.Name}: upgrade without a unit restriction");
            });
        }
        Assert.Contains(roster.Detachments, d => d.Enhancements.Count > 0);
        // Required unit names must be real units, or the enhancement could never be taken.
        var ids = roster.Units.Select(u => u.Id).ToHashSet();
        Assert.All(roster.Detachments.SelectMany(d => d.Enhancements).SelectMany(e => e.RequiredUnitNames), n => Assert.Contains(n, ids));
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Detachment_effects_name_real_units_and_weapons_and_their_keywords_have_rule_text(string file)
    {
        var db = Database.Value;
        var settings = new WarOrganConverter.Emit.BuildSettings("Effect Test", new Version(1, 0, 0, 0),
            Path.Combine(Path.GetTempPath(), "bsdata-to-warorgan-tests", Guid.NewGuid().ToString("N")), [Path.GetFileNameWithoutExtension(file)]);
        var model = WarOrganConverter.Emit.DatasetBuilder.Build(db, settings, new WarOrganConverter.Emit.BuildReport());
        var roster = model.Rosters.Single().Roster;
        var units = roster.Units.ToDictionary(u => u.Id);
        var regexes = model.Definition.WeaponAbilities.Select(a => new System.Text.RegularExpressions.Regex(a.Regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
        var effects = roster.Detachments.SelectMany(d => d.Effects).ToList();
        if (file is "Orks.json" or "Necrons.json") Assert.NotEmpty(effects);        // other catalogues may have none
        foreach (var e in effects)
            foreach (var id in e.AffectedUnits)
            {
                Assert.True(units.ContainsKey(id), $"effect names unknown unit {id}");
                var weapons = units[id].Weapons.SelectMany(w => w.Weapons).Select(w => w.Id!).ToHashSet();
                foreach (var adj in e.WeaponStatAdjustments ?? [])
                {
                    Assert.All(adj.NameFilter!, n => Assert.Contains(n, weapons));
                    if (adj.TargetStatId == "Keywords")
                        Assert.All(WarOrganConverter.Emit.AbilityDefinitions.SplitKeywords(adj.Value), k => Assert.Contains(regexes, r => r.IsMatch(k)));
                }
            }
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Wargear_ids_resolve_and_every_weapon_is_selectable(string file)
    {
        var (units, warnings) = ExtractAll(file);
        Assert.DoesNotContain(warnings, w => w.Contains("not part of any model"));
        foreach (var u in units)
        {
            var known = u.Weapons.SelectMany(w => w.Weapons).Select(w => w.Id!)
                .Concat(u.UnitComposition.WargearDefinitions.Select(d => d.Key)).ToHashSet();
            foreach (var m in u.UnitComposition.ModelCompositions)
                foreach (var g in m.Wargear)
                {
                    var used = g.InitialWargear.Concat(g.Options.SelectMany(o => (o.Replaces ?? []).Concat(o.Options.SelectMany(c => c.Items))));
                    foreach (var id in used) Assert.True(known.Contains(id), $"{u.Id}.{m.ModelName}: unknown wargear id '{id}'");
                }
        }
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Points_conditions_in_scope_are_all_understood(string file)
    {
        var (_, warnings) = ExtractAll(file);
        // Known gap: a unit whose price depends on how many of one optional model type it has (two overlapping size ranges) cannot be
        // priced by total model count alone; the evaluator reports it. Anything else here is a regression.
        Assert.DoesNotContain(warnings, w => w.Contains("points: unsupported") && !w.StartsWith("Windriders:", StringComparison.Ordinal));
    }

    [IntegrationFact]
    public void The_orks_and_necrons_dataset_passes_the_builders_validation_and_is_written()
    {
        var db = Database.Value;
        var settings = new WarOrganConverter.Emit.BuildSettings("Integration Test", new Version(1, 0, 0, 0),
            Path.Combine(Path.GetTempPath(), "bsdata-to-warorgan-tests", Guid.NewGuid().ToString("N")), ["Orks", "Necrons"]);
        var report = new WarOrganConverter.Emit.BuildReport();
        var model = WarOrganConverter.Emit.DatasetBuilder.Build(db, settings, report);
        WarOrganConverter.Emit.DatasetBuilder.Validate(model);            // throws on any validation error
        var zip = WarOrganConverter.Emit.DatasetBuilder.Publish(model, settings, "the integration test");
        Assert.True(new FileInfo(zip).Length > 10_000);
        Assert.Equal(2, report.Rosters);
        Assert.DoesNotContain(report.Warnings, w => w.Contains("no rule text found"));
    }

    [IntegrationTheory]
    [MemberData(nameof(AllFactions))]
    public void Ids_and_names_are_trimmed_as_the_builder_requires(string file)
    {
        var (units, _) = ExtractAll(file);
        foreach (var u in units)
        {
            Assert.Equal(u.Id.Trim(), u.Id);
            foreach (var w in u.Weapons.SelectMany(s => s.Weapons)) Assert.Equal(w.Id!.Trim(), w.Id);
            foreach (var s in u.StatLines) Assert.Equal(s.StatName!.Trim(), s.StatName);
        }
    }

    [IntegrationFact]
    public void Imported_units_of_other_factions_become_allies_and_validate()
    {
        var settings = new WarOrganConverter.Emit.BuildSettings("Ally Test", new Version(1, 0, 0, 0), Path.GetTempPath(),
            ["Imperium - Adeptus Custodes.json", "Imperium - Imperial Knights.json", "Imperium - Agents of the Imperium.json"]);
        var model = WarOrganConverter.Emit.DatasetBuilder.Build(Database.Value, settings, new WarOrganConverter.Emit.BuildReport());
        WarOrganConverter.Emit.DatasetBuilder.Validate(model);
        var custodes = model.Definition.Factions.Single(f => f.Name == "Adeptus Custodes");
        Assert.Contains(custodes.Allies!, a => a.FactionKeyword == "Imperial Knights");
        Assert.Contains(custodes.Allies!, a => a.FactionKeyword == "Agents of the Imperium");
        // The ally roster is not restricted when the faction imports all of it; the Knights roster has more units than Custodes may take? both cases must name only existing units.
        var rosterIds = model.Rosters.SelectMany(r => r.Roster.Units).Select(u => u.Id).ToHashSet();
        foreach (var rule in custodes.Allies!.SelectMany(a => a.UnitIncludeRules ?? []))
            Assert.All(rule.AffectedUnitsFilter!, id => Assert.Contains(id, rosterIds));
    }
}

public class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (string.IsNullOrEmpty(IntegrationTests.Source))
            Skip = "Set BSDATA_SRC to a BSData checkout to run the integration tests.";
    }
}
