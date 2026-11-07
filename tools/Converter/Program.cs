using System.Text.Json;
using WarOrgan.Data.BuildDataset.Utils;
using WarOrgan.Model.DataSet;
using WarOrganConverter.Emit;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

// Usage:
//   warorgan-converter survey  --bsdata <folder> [--faction <catalogue name>]...
//   warorgan-converter extract --bsdata <folder> [--faction <catalogue name>]... [--brief] [--unit <name>]
//   warorgan-converter convert --bsdata <folder> --output <folder> --dataset-id <id> [--dataset-version 1.0.0.0] [--faction <name>]...
// (cache commands follow in later phases, see docs/05-plan.md)
var argsList = args.ToList();
string? Opt(string name)
{
    var i = argsList.IndexOf(name);
    return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null;
}
IEnumerable<string> Opts(string name)
{
    for (var i = 0; i < argsList.Count - 1; i++)
        if (argsList[i] == name) yield return argsList[i + 1];
}

var command = argsList.FirstOrDefault() ?? "help";
if (command is not ("survey" or "extract" or "convert"))
{
    Console.WriteLine("Usage: warorgan-converter survey|extract|convert --bsdata <folder> [--faction <catalogue name>]...");
    Console.WriteLine("  convert also needs --output <folder> --dataset-id <id> [--dataset-version 1.0.0.0]");
    return command == "help" ? 0 : 2;
}

var bsdata = Opt("--bsdata") ?? Environment.GetEnvironmentVariable("BSDATA_SRC");
if (bsdata is null)
{
    Console.Error.WriteLine("--bsdata <folder> is required");
    return 2;
}
var db = BsdDatabase.Load(bsdata);

if (command == "convert")
{
    var id = Opt("--dataset-id");
    var output = Opt("--output");
    if (id is null || output is null || !DataSetValidation.IsValidDataSetId(id))
    {
        Console.Error.WriteLine("convert needs --output <folder> and --dataset-id <id> (3-30 characters: letters, digits, space, - _ ( ) [ ]).");
        return 2;
    }
    if (!Version.TryParse(Opt("--dataset-version") ?? "1.0.0.0", out var version))
    {
        Console.Error.WriteLine("--dataset-version must look like 1.0.0.0");
        return 2;
    }
    var settings = new BuildSettings(id, version, output, Opts("--faction").ToList());
    var report = new BuildReport();
    try
    {
        var model = DatasetBuilder.Build(db, settings, report);
        DatasetBuilder.Validate(model);
        report.ZipPath = DatasetBuilder.Publish(model, settings, $"the BSData catalogues in {Path.GetFullPath(bsdata)}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Conversion failed: {ex.Message}");
    }
    Console.WriteLine($"Converted {report.Units} units in {report.Rosters} roster(s).");
    foreach (var w in report.Warnings) Console.WriteLine("  warning: " + w);
    LoggingBuffer.Print(waitForInput: false);
    if (report.ZipPath is not null) Console.WriteLine($"{Environment.NewLine}Dataset written: {report.ZipPath}");
    return report.ZipPath is not null && LoggingBuffer.ErrorCount == 0 ? 0 : 1;
}

var resolver = new RosterResolver(db);
var factions = Opts("--faction").ToList();
foreach (var (file, root) in db.Roots.Where(kv => BsdDatabase.Str(kv.Value, "type") != "gameSystem" && !Scope.IsExcludedFile(kv.Key)))
{
    var name = BsdDatabase.Str(root, "name")!;
    if (factions.Count > 0 && !factions.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
    var units = resolver.Resolve(file);
    if (command == "extract")
    {
        var warnings = new List<string>();
        var roster = RosterExtractor.Extract(db, file, warnings);
        var armyRules = roster.ArmyRules;
        Console.WriteLine($"  army rules: {string.Join(", ", armyRules.Select(db.Name))}");
        var extracted = roster.Units;
        Console.WriteLine($"  keywords {extracted.Sum(u => u.Keywords.Count)}, faction keywords {extracted.Sum(u => u.FactionKeywords.Count)}, core abilities {extracted.Sum(u => u.CoreAbilities.Count)}, unit abilities {extracted.Sum(u => u.UnitAbilities.Count)}");
        Console.WriteLine($"  core abilities used: {string.Join("; ", extracted.SelectMany(u => u.CoreAbilities).Distinct().Order())}");
        Console.WriteLine($"  detachments {roster.Detachments.Count}, enhancements {roster.Detachments.Sum(d => d.Enhancements.Count)}");
        if (argsList.Contains("--detachments"))
            foreach (var d in roster.Detachments)
                Console.WriteLine($"  {d.Name,-26} {d.Cost}DP [{string.Join(", ", d.ForceDispositions ?? [])}] rule '{d.Rule?.Title}' enh: {string.Join("; ", d.Enhancements.Select(x => $"{x.Name} {x.Cost}{(x.Features.HasFlag(WarOrgan.Model.Faction.EnhancementFeature.Upgrade) ? "U" : "")}"))}");
        Console.WriteLine($"{name}: {extracted.Count} units, {extracted.Sum(u => u.StatLines.Count)} statlines, {extracted.Sum(u => u.Weapons.Sum(w => w.Weapons.Count))} weapon profiles");
        foreach (var w in warnings) Console.WriteLine("  warning: " + w);
        if (argsList.Contains("--brief"))
            foreach (var u in extracted)
                Console.WriteLine($"  {u.DisplayName,-34} models [{string.Join(", ", u.UnitComposition.ModelCompositions.Select(m => $"{m.ModelName} {m.Limit?.Min}-{m.Limit?.Max}"))}] pts [{string.Join(", ", u.Points.Select(p => $"{(p.UnitCount is null ? "" : "u<=" + p.UnitCount + " ")}m<={p.ModelCount}:{p.Cost}"))}]");
        var sample = Opt("--unit");
        var show = extracted.FirstOrDefault(u => string.Equals(u.DisplayName, sample, StringComparison.OrdinalIgnoreCase));
        if (show is not null) Console.WriteLine(JsonSerializer.Serialize(show, new JsonSerializerOptions { WriteIndented = true }));
        continue;
    }
    Console.WriteLine($"{name,-40} units={units.Count,3} (imported {units.Count(u => u.Imported)})");
}
return 0;
