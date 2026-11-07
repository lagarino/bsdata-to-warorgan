using System.Text.Json.Nodes;
using WarOrgan.Model.Unit;
using WarOrganConverter.Load;

namespace WarOrganConverter.Extract;

/// <summary>
/// Points tiers of a unit. The BSData pts cost is a base value changed by modifiers; the converter evaluates the
/// modifiers it understands for every (model count, earlier copies) scenario and writes the result as
/// PointsDefinition ceilings (docs/07-app-behaviour.md, "Points"). Modifiers it does not understand are reported.
/// </summary>
public sealed class PointsExtractor
{
    public const string PtsTypeId = "51b2-306e-1021-d207";

    private readonly record struct Scenario(int Models, int CopiesBefore);

    public sealed record Result(List<PointsDefinition> Points, List<string> Unsupported);

    public Result Extract(JsonObject unitEntry, string unitName, int minModels, int maxModels)
    {
        var unsupported = new List<string>();
        var baseCost = BaseCost(unitEntry);
        var modifiers = PointModifiers(unitEntry).ToList();
        var maxCopies = 0;
        foreach (var m in modifiers) maxCopies = Math.Max(maxCopies, CopyThreshold(m));

        var models = Enumerable.Range(Math.Max(1, minModels), Math.Max(1, maxModels) - Math.Max(1, minModels) + 1).ToList();
        var grid = new int[models.Count, maxCopies + 1];
        for (var i = 0; i < models.Count; i++)
            for (var c = 0; c <= maxCopies; c++)
                grid[i, c] = Evaluate(baseCost, modifiers, new Scenario(models[i], c), unsupported);

        // Group copy counts with identical cost columns; the group's UnitCount is its highest copy index (ceiling).
        var copyRuns = new List<(int Last, int Column)>();
        for (var c = 0; c <= maxCopies; c++)
            if (c == maxCopies || !Column(grid, models.Count, c).SequenceEqual(Column(grid, models.Count, c + 1)))
                copyRuns.Add((c, c));

        var result = new List<PointsDefinition>();
        foreach (var (last, col) in copyRuns)
        {
            // Within a copy run, collapse model counts with equal cost; ModelCount is the highest count of the run.
            for (var i = 0; i < models.Count; i++)
            {
                if (i < models.Count - 1 && grid[i, col] == grid[i + 1, col]) continue;
                result.Add(new PointsDefinition
                {
                    ModelCount = models[i],
                    Cost = grid[i, col],
                    UnitCount = maxCopies > 0 ? last + 1 : null,
                });
            }
        }
        if (result.Any(p => p.Cost <= 0)) unsupported.Add("a points tier evaluated to 0 or less");
        return new Result(result, unsupported.Distinct().ToList());
    }

    private static IEnumerable<int> Column(int[,] grid, int rows, int c)
    {
        for (var i = 0; i < rows; i++) yield return grid[i, c];
    }

    private static int BaseCost(JsonObject entry) =>
        BsdDatabase.Arr(entry, "costs").Where(c => BsdDatabase.Str(c, "typeId") == PtsTypeId)
            .Select(c => c["value"] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0).FirstOrDefault();

    /// <summary>Modifiers that change the pts cost, with the conditions of their enclosing modifier groups folded in.</summary>
    private static IEnumerable<(JsonObject Modifier, List<JsonObject> Groups)> PointModifiers(JsonObject entry)
    {
        foreach (var m in BsdDatabase.Arr(entry, "modifiers"))
            if (BsdDatabase.Str(m, "field") == PtsTypeId) yield return (m, []);
        foreach (var g in BsdDatabase.Arr(entry, "modifierGroups"))
            foreach (var x in Flatten(g, []))
                yield return x;
    }

    private static IEnumerable<(JsonObject, List<JsonObject>)> Flatten(JsonObject group, List<JsonObject> outer)
    {
        var chain = new List<JsonObject>(outer) { group };
        foreach (var m in BsdDatabase.Arr(group, "modifiers"))
            if (BsdDatabase.Str(m, "field") == PtsTypeId) yield return (m, chain);
        foreach (var g in BsdDatabase.Arr(group, "modifierGroups"))
            foreach (var x in Flatten(g, chain)) yield return x;
    }

    private static int Evaluate(int cost, List<(JsonObject Modifier, List<JsonObject> Groups)> modifiers, Scenario s, List<string> unsupported)
    {
        foreach (var (m, groups) in modifiers)
        {
            var applies = true;
            foreach (var g in groups)
                applies &= Holds(g, s, unsupported) ?? false;
            if (applies) applies = Holds(m, s, unsupported) ?? false;
            if (!applies) continue;
            if (m["repeats"] is not null) { unsupported.Add("pts modifier with repeats"); continue; }
            if (m["value"] is not JsonValue v || !v.TryGetValue<double>(out var d)) continue;
            switch (BsdDatabase.Str(m, "type"))
            {
                case "set": cost = (int)d; break;
                case "increment": cost += (int)d; break;
                case "decrement": cost -= (int)d; break;
            }
        }
        return cost;
    }

    /// <summary>Largest "earlier copies" threshold used by any copy-index condition below this modifier.</summary>
    private static int CopyThreshold(( JsonObject Modifier, List<JsonObject> Groups) m)
    {
        var max = 0;
        void Visit(JsonObject n)
        {
            foreach (var g in BsdDatabase.Arr(n, "conditionGroups").Concat(BsdDatabase.Arr(n, "localConditionGroups")))
            {
                if (IsCopyGroup(g) && g["value"] is JsonValue v && v.TryGetValue<double>(out var d)) max = Math.Max(max, (int)d);
                Visit(g);
            }
        }
        Visit(m.Modifier);
        foreach (var g in m.Groups) Visit(g);
        return max;
    }

    private static bool IsCopyGroup(JsonObject g) =>
        BsdDatabase.Arr(g, "conditions").Any(c => BsdDatabase.Str(c, "type") == "before");

    /// <summary>True/false, or null when a condition is outside what the evaluator understands.</summary>
    private static bool? Holds(JsonObject node, Scenario s, List<string> unsupported)
    {
        var results = new List<bool?>();
        foreach (var c in BsdDatabase.Arr(node, "conditions"))
            results.Add(Condition(c, s, unsupported));
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups"))
            results.Add(Holds(g, s, unsupported));
        foreach (var g in BsdDatabase.Arr(node, "localConditionGroups"))
            results.Add(IsCopyGroup(g) ? CopyGroup(g, s) : Unsupported(unsupported, "local condition group"));
        if (results.Count == 0) return true;
        if (results.Any(r => r is null)) return null;
        return BsdDatabase.Str(node, "type") == "or" ? results.Any(r => r == true) : results.All(r => r == true);
    }

    private static bool? CopyGroup(JsonObject g, Scenario s)
    {
        if (g["value"] is not JsonValue v || !v.TryGetValue<double>(out var d)) return null;
        return Compare(BsdDatabase.Str(g, "type"), s.CopiesBefore, d);
    }

    private static bool? Condition(JsonObject c, Scenario s, List<string> unsupported)
    {
        var field = BsdDatabase.Str(c, "field");
        var child = BsdDatabase.Str(c, "childId");
        var scope = BsdDatabase.Str(c, "scope");
        if (field == "selections" && child == "model" && c["value"] is JsonValue v && v.TryGetValue<double>(out var d)
            && scope is not ("force" or "roster" or "primary-catalogue" or "root-entry" or "parent"))
            return Compare(BsdDatabase.Str(c, "type"), s.Models, d);
        return Unsupported(unsupported, $"{BsdDatabase.Str(c, "type")} {field} child={(child is "model" or "any" ? child : "id")} scope={(scope is "self" or "parent" or "force" or "roster" or "primary-catalogue" or "root-entry" ? scope : "unit")}");
    }

    private static bool? Unsupported(List<string> list, string reason)
    {
        list.Add(reason);
        return null;
    }

    private static bool? Compare(string? type, double actual, double value) => type switch
    {
        "atLeast" => actual >= value,
        "greaterThan" => actual > value,
        "atMost" => actual <= value,
        "lessThan" => actual < value,
        "equalTo" => actual == value,
        "notEqualTo" => actual != value,
        _ => null,
    };
}
