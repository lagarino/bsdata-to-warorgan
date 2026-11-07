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

    /// <summary>
    /// What the evaluator needs to know beyond the unit entry: how to name a referenced entry, which names stand for "all models of the
    /// unit" (a unit with one kind of model), and a per-model cost when the price sits on the model entries.
    /// </summary>
    public sealed record Context(Func<string?, string?> NameOf, IReadOnlySet<string> WholeUnitNames, int? PerModelCost);

    /// <summary>Entries of campaign or alternative modes: never active in a standard game.</summary>
    private static bool IsOutOfScopeMode(string? name) =>
        name is not null && (name.Contains("Crusade", StringComparison.OrdinalIgnoreCase) || name.Contains("Boarding Actions", StringComparison.OrdinalIgnoreCase));

    private static readonly System.Text.RegularExpressions.Regex SizeUpgrade = new(@"^\s*(\d+)\s+models?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public sealed record Result(List<PointsDefinition> Points, List<string> Unsupported);

    public Result Extract(JsonObject unitEntry, string unitName, int minModels, int maxModels, Context? context = null)
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
                grid[i, c] = Evaluate(baseCost > 0 || context?.PerModelCost is null ? baseCost : context.PerModelCost.Value * models[i], modifiers, new Scenario(models[i], c), unsupported, context);

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
        if (result.Any(p => p.Cost < 0)) unsupported.Add("a points tier evaluated below 0");
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

    private static int Evaluate(int cost, List<(JsonObject Modifier, List<JsonObject> Groups)> modifiers, Scenario s, List<string> unsupported, Context? context)
    {
        foreach (var (m, groups) in modifiers)
        {
            // Unknown conditions are only reported when they decide the outcome (an "and" with a known false needs no answer).
            var reasons = new List<string>();
            bool? applies = true;
            foreach (var g in groups) applies = And(applies, Holds(g, s, reasons, context));
            applies = And(applies, Holds(m, s, reasons, context));
            if (applies is null) unsupported.AddRange(reasons);
            if (applies != true) continue;
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
    private static bool? And(bool? a, bool? b) => a == false || b == false ? false : a is null || b is null ? null : true;

    private static bool? Holds(JsonObject node, Scenario s, List<string> unsupported, Context? context)
    {
        var results = new List<bool?>();
        foreach (var c in BsdDatabase.Arr(node, "conditions"))
            results.Add(Condition(c, s, unsupported, context));
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups"))
            results.Add(Holds(g, s, unsupported, context));
        foreach (var g in BsdDatabase.Arr(node, "localConditionGroups"))
            results.Add(IsCopyGroup(g) ? CopyGroup(g, s) : Unsupported(unsupported, "local condition group"));
        if (results.Count == 0) return true;
        // A known answer decides a group even when another condition is unknown: any true for "or", any false for "and".
        if (BsdDatabase.Str(node, "type") == "or")
            return results.Any(r => r == true) ? true : results.Any(r => r is null) ? null : false;
        return results.Any(r => r == false) ? false : results.Any(r => r is null) ? null : true;
    }

    private static bool? CopyGroup(JsonObject g, Scenario s)
    {
        if (g["value"] is not JsonValue v || !v.TryGetValue<double>(out var d)) return null;
        return Compare(BsdDatabase.Str(g, "type"), s.CopiesBefore, d);
    }

    private static bool? Condition(JsonObject c, Scenario s, List<string> unsupported, Context? context)
    {
        var field = BsdDatabase.Str(c, "field");
        var child = BsdDatabase.Str(c, "childId");
        var scope = BsdDatabase.Str(c, "scope");
        var childName = child is null or "model" or "any" ? null : context?.NameOf(child);
        if (IsOutOfScopeMode(childName))
            return BsdDatabase.Str(c, "type") is "notInstanceOf" or "lessThan" or "atMost" or "equalTo" && (c["value"]?.ToString() is "0" || BsdDatabase.Str(c, "type") != "equalTo");
        // A unit size chosen through an upgrade entry named "10 models": selected when the unit has that many models.
        if (field == "selections" && childName is not null && SizeUpgrade.Match(childName) is { Success: true } sz && c["value"] is JsonValue sv && sv.TryGetValue<double>(out var svd)
            && scope is not ("force" or "roster" or "primary-catalogue" or "root-entry"))
            return Compare(BsdDatabase.Str(c, "type"), s.Models == int.Parse(sz.Groups[1].Value) ? 1 : 0, svd);
        // The count of the one kind of model a unit has is the unit's model count.
        if (field == "selections" && childName is not null && context!.WholeUnitNames.Contains(childName) && c["value"] is JsonValue wv && wv.TryGetValue<double>(out var wd)
            && scope is not ("force" or "roster" or "primary-catalogue" or "root-entry" or "parent"))
            return Compare(BsdDatabase.Str(c, "type"), s.Models, wd);
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
