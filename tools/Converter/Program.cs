using System.Text.Json;
using WarOrganConverter.Extract;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

// Usage: warorgan-converter survey|extract --bsdata <folder> [--faction <catalogue name>]...
// (convert and cache commands follow in later phases, see docs/05-plan.md)
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
if (command is not ("survey" or "extract"))
{
    Console.WriteLine("Usage: warorgan-converter survey|extract --bsdata <folder> [--faction <catalogue name>]...");
    return command == "help" ? 0 : 2;
}

var bsdata = Opt("--bsdata") ?? Environment.GetEnvironmentVariable("WH40K_SRC");
if (bsdata is null)
{
    Console.Error.WriteLine("--bsdata <folder> is required");
    return 2;
}
var db = BsdDatabase.Load(bsdata);
var resolver = new RosterResolver(db);
var factions = Opts("--faction").ToList();
foreach (var (file, root) in db.Roots.Where(kv => BsdDatabase.Str(kv.Value, "type") != "gameSystem" && !Scope.IsExcludedFile(kv.Key)))
{
    var name = BsdDatabase.Str(root, "name")!;
    if (factions.Count > 0 && !factions.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
    var units = resolver.Resolve(file);
    if (command == "extract")
    {
        var extractor = new UnitExtractor(db);
        var warnings = new List<string>();
        var armyRules = UnitExtractor.FindArmyRules(units);
        Console.WriteLine($"  army rules: {string.Join(", ", armyRules.Select(db.Name))}");
        var extracted = units.Select(u => extractor.Extract(u, armyRules, warnings)).ToList();
        Console.WriteLine($"  keywords {extracted.Sum(u => u.Keywords.Count)}, faction keywords {extracted.Sum(u => u.FactionKeywords.Count)}, core abilities {extracted.Sum(u => u.CoreAbilities.Count)}, unit abilities {extracted.Sum(u => u.UnitAbilities.Count)}");
        Console.WriteLine($"  core abilities used: {string.Join("; ", extracted.SelectMany(u => u.CoreAbilities).Distinct().Order())}");
        Console.WriteLine($"{name}: {extracted.Count} units, {extracted.Sum(u => u.StatLines.Count)} statlines, {extracted.Sum(u => u.Weapons.Sum(w => w.Weapons.Count))} weapon profiles");
        foreach (var w in warnings) Console.WriteLine("  warning: " + w);
        var sample = Opt("--unit");
        var show = extracted.FirstOrDefault(u => string.Equals(u.DisplayName, sample, StringComparison.OrdinalIgnoreCase));
        if (show is not null) Console.WriteLine(JsonSerializer.Serialize(show, new JsonSerializerOptions { WriteIndented = true }));
        continue;
    }
    Console.WriteLine($"{name,-40} units={units.Count,3} (imported {units.Count(u => u.Imported)})");
}
return 0;
