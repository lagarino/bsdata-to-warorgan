using System.Text;
using System.Text.Json.Nodes;
using WarOrgan.Data.BuildDataset.Utils;
using WarOrgan.Data.BuildDataset.Validation;
using WarOrgan.Model.DataSet;
using WarOrgan.Model.Faction;
using WarOrgan.Model.Unit;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Emit;

public sealed record BuildSettings(
    string DataSetId,
    Version Version,
    string OutputPath,
    IReadOnlyList<string> Catalogues);

public sealed class BuildReport
{
    public List<string> Warnings { get; } = [];
    public List<string> Notes { get; } = [];
    public int Units { get; set; }
    public int Rosters { get; set; }
    public string? ZipPath { get; set; }
}

/// <summary>Stage 8: turns extracted rosters into a DataSetDefinition and rosters, validates them and writes the zip.</summary>
public static class DatasetBuilder
{
    /// <summary>Unit keywords the app groups units by; only those present in the data are emitted (the builder requires a match).</summary>
    private static readonly string[] CategoryOrder =
        ["Epic Hero", "Character", "Battleline", "Infantry", "Mounted", "Beast", "Monster", "Vehicle", "Swarm", "Dedicated Transport", "Fortification"];

    private static readonly Dictionary<string, (string Main, string Dark)> FactionColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Orks"] = ("#4C7A34", "#2B4A1E"),
        ["Necrons"] = ("#3FA773", "#1E5238"),
    };

    private static readonly (string Main, string Dark)[] FallbackColors =
    [
        ("#5B7DB1", "#2F4568"), ("#B15B5B", "#682F2F"), ("#B1A05B", "#686030"), ("#8A5BB1", "#512F68"),
        ("#5BB1A8", "#2F6862"), ("#B1795B", "#684630"),
    ];

    public static List<BattleSizeDefinition> BattleSizes() =>
    [
        new("Incursion", 1000, 2, 2, 3),
        new("Strike Force", 2000, 3, 4, 3),
        new("Onslaught", 3000, 4, 4, 3),
    ];

    /// <summary>Converts the selected catalogues and returns the validated dataset (before it is written).</summary>
    public static DatasetBuildModel Build(BsdDatabase db, BuildSettings settings, BuildReport report)
    {
        var rules = SourceRules(db);
        var rosters = new List<FactionRoster>();
        var factions = new List<FactionInfo>();

        foreach (var (file, root) in SelectCatalogues(db, settings.Catalogues))
        {
            var catalogueName = BsdDatabase.Str(root, "name") ?? file;
            var warnings = new List<string>();
            var extracted = RosterExtractor.Extract(db, file, warnings);
            var army = extracted.ArmyRules;
            var roster = new FactionRoster { Name = DisplayName(catalogueName) };
            foreach (var unit in extracted.Units)
            {
                foreach (var set in unit.Weapons)
                    foreach (var w in set.Weapons)
                        w.Keywords = NullIfEmpty(AbilityDefinitions.NormaliseKeywords(w.Keywords));
                roster.Units.Add(unit);
            }
            roster.Detachments.AddRange(extracted.Detachments);
            foreach (var id in army.Order())
                if (db.ById.TryGetValue(id, out var r) && BsdDatabase.Str(r.Node, "description") is { } text && !string.IsNullOrWhiteSpace(text))
                    roster.ArmyRules.Add(new UnitInfoSection { Title = BsdDatabase.Str(r.Node, "name")!.Trim(), Text = text.Trim() });
            foreach (var w in warnings) report.Warnings.Add($"{roster.Name}: {w}");
            rosters.Add(roster);
            factions.Add(FactionFor(catalogueName, roster));
            report.Units += roster.Units.Count;
        }
        if (rosters.Count == 0) throw new InvalidOperationException("No catalogues matched; nothing to convert.");
        report.Rosters = rosters.Count;

        var unmatched = new List<string>();
        var definition = new DataSetDefinition
        {
            Features = DataSetFeatures.ArmyRules
                       | (rosters.Any(r => r.Detachments.Count > 0) ? DataSetFeatures.Detachments | DataSetFeatures.Dispositions : 0)
                       | (rosters.Any(r => r.Detachments.Any(d => d.Enhancements.Count > 0)) ? DataSetFeatures.Enhancements : 0),
            Factions = factions,
            BattleSizeDefinitions = BattleSizes(),
            CoreAbilities = AbilityDefinitions.Build(rosters.SelectMany(r => r.Units).SelectMany(u => u.CoreAbilities), rules, unmatched),
            WeaponAbilities = AbilityDefinitions.Build(
                rosters.SelectMany(r => r.Units).SelectMany(u => u.Weapons).SelectMany(s => s.Weapons).SelectMany(w => AbilityDefinitions.SplitKeywords(w.Keywords))
                    .Concat(rosters.SelectMany(r => r.Detachments).SelectMany(d => d.Effects).SelectMany(e => e.WeaponStatAdjustments ?? [])
                        .Where(a => a.TargetStatId == "Keywords").SelectMany(a => AbilityDefinitions.SplitKeywords(a.Value))),
                rules, unmatched),
        };
        var present = rosters.SelectMany(r => r.Units).SelectMany(u => u.Keywords).ToHashSet(StringComparer.Ordinal);
        definition.CategoryKeywords = CategoryOrder.Where(present.Contains).Select(k => new CategoryKeyword(k)).ToList();
        foreach (var t in unmatched.Distinct()) report.Warnings.Add($"no rule text found for ability or keyword '{t}'");

        var info = new DataSetInfo(settings.DataSetId, settings.Version, DataSetInfo.CurrentDataSetFormatVersion, DateTime.UtcNow);
        var model = new DatasetBuildModel(info, definition, rosters.Select(r => new FactionRosterEntry(r)).ToList(), []);
        return model;
    }

    /// <summary>Runs the builder's validation; throws when the dataset is not valid.</summary>
    public static void Validate(DatasetBuildModel model) => model.Validate();

    /// <summary>Writes the zip (and a README with the provenance notice) into the output folder.</summary>
    public static string Publish(DatasetBuildModel model, BuildSettings settings, string sourceDescription)
    {
        var output = settings.OutputPath.EnsureDirectoryExists();
        var readme = Path.Combine(output, "README.md");
        File.WriteAllText(readme, ReadmeText(model, sourceDescription), new UTF8Encoding(false));
        model.WithAdditionalFiles(readme).PublishToZipArchive(output);
        return Path.Combine(output, $"{model.Info.Id}.zip");
    }

    private static string ReadmeText(DatasetBuildModel model, string source)
    {
        return $"""
            # {model.Info.Id}

            Generated by bsdata-to-warorgan on {DateTime.UtcNow:yyyy-MM-dd} from {source}.

            This dataset contains game content converted from community BattleScribe (BSData) catalogues.
            It was generated for personal use on the machine of whoever ran the converter. The game content belongs to
            its rights holders and the catalogues to their authors; do not publish or redistribute this file.
            Not affiliated with or endorsed by WarOrgan, BSData or any games publisher.
            """;
    }

    private static IEnumerable<(string File, JsonObject Root)> SelectCatalogues(BsdDatabase db, IReadOnlyList<string> wanted)
    {
        foreach (var (file, root) in db.Roots)
        {
            if (BsdDatabase.Str(root, "type") == "gameSystem" || Scope.IsExcludedFile(file)) continue;
            if (root["library"] is JsonValue v && v.TryGetValue<bool>(out var lib) && lib) continue;
            var name = BsdDatabase.Str(root, "name") ?? file;
            if (wanted.Count > 0 && !wanted.Any(w => w.Equals(name, StringComparison.OrdinalIgnoreCase) || w.Equals(DisplayName(name), StringComparison.OrdinalIgnoreCase)
                                                    || w.Equals(Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)))
                continue;
            yield return (file, root);
        }
    }

    /// <summary>Shared rules of the game system(s): name to description.</summary>
    private static Dictionary<string, string> SourceRules(BsdDatabase db)
    {
        var rules = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in db.Roots.Values.Where(r => BsdDatabase.Str(r, "type") == "gameSystem"))
            foreach (var r in BsdDatabase.Arr(root, "sharedRules"))
                if (BsdDatabase.Str(r, "name")?.Trim() is { Length: > 0 } name)
                    rules.TryAdd(name, BsdDatabase.Str(r, "description") ?? "");
        return rules;
    }

    /// <summary>"Xenos - Orks" becomes "Orks".</summary>
    public static string DisplayName(string catalogueName)
    {
        var i = catalogueName.IndexOf(" - ", StringComparison.Ordinal);
        return (i >= 0 ? catalogueName[(i + 3)..] : catalogueName).Trim();
    }

    private static FactionInfo FactionFor(string catalogueName, FactionRoster roster)
    {
        var i = catalogueName.IndexOf(" - ", StringComparison.Ordinal);
        var keyword = roster.Units.SelectMany(u => u.FactionKeywords).Contains(roster.Name)
            ? roster.Name
            : roster.Units.SelectMany(u => u.FactionKeywords).GroupBy(k => k).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? roster.Name;
        var (main, dark) = FactionColors.TryGetValue(roster.Name, out var c) ? c : FallbackColors[roster.Name.Sum(ch => (int)ch) % FallbackColors.Length];
        return new FactionInfo
        {
            Name = roster.Name,
            FactionKeyword = keyword,
            Category = i >= 0 ? catalogueName[..i].Trim() : "Other",
            MainColor = main,
            DarkColor = dark,
        };
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}
