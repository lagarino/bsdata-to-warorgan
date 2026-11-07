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
        var allyUnits = new List<(string Roster, Dictionary<string, List<string>> ByFaction, string? Parent)>();
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
            allyUnits.Add((roster.Name, extracted.ImportedByFaction ?? [], extracted.ParentKeyword));
            var factionInfo = FactionFor(catalogueName, roster);
            // A datasheet with no faction category (some characters) belongs to the roster it is listed in; the builder needs a keyword.
            foreach (var unit in roster.Units.Where(u => u.FactionKeywords.Count == 0))
                unit.FactionKeywords.Add(factionInfo.FactionKeyword);
            factions.Add(factionInfo);
            report.Units += roster.Units.Count;
        }
        if (rosters.Count == 0) throw new InvalidOperationException("No catalogues matched; nothing to convert.");
        report.Rosters = rosters.Count;
        AddAllies(factions, rosters, allyUnits, report);

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
        TextCleaner.CleanAll(model);
        return model;
    }

    /// <summary>
    /// A faction that imports units of another faction gets that faction as an ally, when the ally's roster is part of the dataset.
    /// The units it may take are listed in an include rule when they are not the whole roster of the ally.
    /// </summary>
    private static void AddAllies(List<FactionInfo> factions, List<FactionRoster> rosters, List<(string Roster, Dictionary<string, List<string>> ByFaction, string? Parent)> imports, BuildReport report)
    {
        foreach (var (rosterName, byFaction, parentKeyword) in imports)
        {
            var faction = factions.First(f => f.Name == rosterName);
            // A unit the catalogue links itself but that belongs to another faction of the dataset (only that faction's keyword) is an allied unit too.
            var own = rosters.First(r => r.Name == rosterName);
            var otherKeywords = factions.Where(f => f != faction).Select(f => f.FactionKeyword).ToHashSet(StringComparer.Ordinal);
            var foreign = own.Units.Where(u => u.FactionKeywords.Count > 0 && !u.FactionKeywords.Contains(faction.FactionKeyword) && u.FactionKeywords.All(otherKeywords.Contains)).ToList();
            foreach (var u in foreign)
            {
                own.Units.Remove(u);
                var key = u.FactionKeywords[0];
                if (!byFaction.TryGetValue(key, out var list)) byFaction[key] = list = [];
                list.Add(u.Id);
            }
            if (foreign.Count > 0) faction.AdditionalFactionKeywords = faction.AdditionalFactionKeywords?.Where(k => !otherKeywords.Contains(k)).ToList() is { Count: > 0 } kept ? kept : null;
            foreach (var (keyword, ids) in byFaction.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var allyFaction = factions.FirstOrDefault(f => f.FactionKeyword == keyword && f != faction);
                if (allyFaction is null)
                {
                    report.Warnings.Add($"{rosterName}: {ids.Count} units of '{keyword}' are imported; that faction is not part of this dataset, so they are left out (add its catalogue to include them)");
                    continue;
                }
                var allyRoster = rosters.First(r => r.Name == allyFaction.Name);
                var allowed = ids.Where(id => allyRoster.Units.Any(u => u.Id == id)).Distinct().ToList();
                if (allowed.Count == 0) continue;
                // The parent faction of a sub-faction (a chapter of the parent army) is part of its own roster.
                var ally = new FactionAlly { FactionKeyword = keyword, IsIncludedInPrimaryRoster = keyword == parentKeyword };
                if (allowed.Count < allyRoster.Units.Count)
                    ally.UnitIncludeRules = [new UnitIncludeRule { AffectedUnitsFilter = allowed }];
                (faction.Allies ??= []).Add(ally);
            }
        }
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
                                                    || w.Equals(Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)
                                                    || w.Equals(Path.GetFileName(file), StringComparison.OrdinalIgnoreCase)))
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
        var i = catalogueName.LastIndexOf(" - ", StringComparison.Ordinal);
        return (i >= 0 ? catalogueName[(i + 3)..] : catalogueName).Trim();
    }

    private static FactionInfo FactionFor(string catalogueName, FactionRoster roster)
    {
        var i = catalogueName.LastIndexOf(" - ", StringComparison.Ordinal);
        var keyword = roster.Units.SelectMany(u => u.FactionKeywords).Contains(roster.Name)
            ? roster.Name
            : roster.Units.SelectMany(u => u.FactionKeywords).GroupBy(k => k).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? roster.Name;
        var (main, dark) = FactionColors.TryGetValue(roster.Name, out var c) ? c : FallbackColors[roster.Name.Sum(ch => (int)ch) % FallbackColors.Length];
        // Units that carry only another faction keyword (for example an allied army's units a catalogue lists itself) need that keyword
        // listed, or the builder reports them as unobtainable.
        var extra = roster.Units.Where(u => !u.FactionKeywords.Contains(keyword)).SelectMany(u => u.FactionKeywords).Distinct().ToList();
        return new FactionInfo
        {
            Name = roster.Name,
            FactionKeyword = keyword,
            AdditionalFactionKeywords = extra.Count > 0 ? extra : null,
            Category = i >= 0 ? catalogueName[..i].Trim() : "Other",
            MainColor = main,
            DarkColor = dark,
        };
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}
