using System.Text.Json.Nodes;
using WarOrgan.Model.Unit;
using WarOrgan.Model.Unit.Wargear;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>
/// Model compositions of a unit: one ModelComposition per BSData model entry, with min/max limits taken from the entry's
/// own "selections" constraints and from the single-model group that contains it (docs/03-mapping.md).
/// Wargear and starting equipment are added by a later stage.
/// </summary>
public sealed class CompositionExtractor(BsdDatabase db)
{
    private readonly EntryWalker _walker = new(db);
    private Dictionary<ModelComposition, JsonObject> _sources = new();
    private string? _fallbackStatline;
    private Dictionary<ModelComposition, Member> _members = new();

    /// <summary>Where a model entry sat: the group whose total limit it shares, an optional nested sub-group, and whether it is the group default.</summary>
    public sealed record Member(JsonObject? Group, JsonObject? SubGroup, int? GroupMin, int? GroupMax, int? SubGroupMax, bool IsDefault);

    public sealed record Result(List<ModelComposition> Models, int MinModels, int MaxModels, Dictionary<ModelComposition, JsonObject> Sources,
        Dictionary<ModelComposition, Member> Members);

    public Result Extract(JsonObject unitEntry, List<string> warnings)
    {
        var name = BsdDatabase.Str(unitEntry, "name") ?? "?";
        var models = new List<ModelComposition>();
        _sources = new Dictionary<ModelComposition, JsonObject>();
        _members = new Dictionary<ModelComposition, Member>();
        _fallbackStatline = OwnUnitProfileName(unitEntry) ?? _walker.Profiles(unitEntry, "Unit").Select(f => BsdDatabase.Str(f.Profile, "name")).FirstOrDefault(n => n is not null);
        int min = 0, max = 0;

        if (BsdDatabase.Str(unitEntry, "type") == "model")
        {
            // A top-level model entry is a single-model datasheet.
            models.Add(Compose(unitEntry, name, 1, 1));
            return new Result(models, 1, 1, _sources, _members);
        }

        Process(unitEntry, models, ref min, ref max, name);

        // The builder needs unique model names; two entries with one name are kept apart with a numeric suffix.
        foreach (var dup in models.GroupBy(m => m.ModelName).Where(g => g.Count() > 1).ToList())
        {
            var i = 1;
            foreach (var m in dup.Skip(1)) m.ModelName = $"{dup.Key} ({++i})";
            warnings.Add($"{name}: model name '{dup.Key}' appears {dup.Count()} times; renamed the later ones");
        }

        if (models.Count == 0) warnings.Add($"{name}: no model entries found");
        if (max < min) { warnings.Add($"{name}: inconsistent model limits ({min}-{max})"); max = min; }
        return new Result(models, min, max, _sources, _members);
    }

    /// <summary>Collects models directly under the node and inside groups; groups without models of their own are descended into.</summary>
    private void Process(JsonObject node, List<ModelComposition> models, ref int min, ref int max, string unitName)
    {
        foreach (var child in Children(node))
        {
            if (BsdDatabase.Str(child, "type") == "model")
            {
                var (mn, mx) = Limits(child);
                var lo = mn ?? 0;
                var hi = mx ?? lo;
                models.Add(Compose(child, BsdDatabase.Str(child, "name") ?? unitName, lo, hi));
                min += lo; max += hi;
            }
            else if (IsGroup(child))
            {
                var inner = Children(child).Where(c => BsdDatabase.Str(c, "type") == "model").ToList();
                if (inner.Count == 0) { Process(child, models, ref min, ref max, unitName); continue; }
                var (gmn, gmx) = Limits(child);
                int sumMin = 0, sumMax = 0;
                foreach (var model in inner)
                {
                    var (mn, mx) = Limits(model);
                    var lo = mn ?? 0;
                    var hi = mx ?? gmx ?? lo;
                    if (inner.Count == 1)
                    {
                        lo = Math.Max(lo, gmn ?? 0);
                        hi = gmx is { } g ? Math.Min(hi, g) : hi;
                    }
                    else if (gmx is { } gm) hi = Math.Min(hi, gm);
                    var composed = Compose(model, BsdDatabase.Str(model, "name") ?? unitName, lo, hi);
                    models.Add(composed);
                    _members[composed] = new Member(child, null, gmn, gmx, null, BsdDatabase.Str(child, "defaultSelectionEntryId") == BsdDatabase.Str(model, "id"));
                    sumMin += lo; sumMax += hi;
                }
                min += gmn ?? sumMin;
                max += gmx is { } gx ? Math.Min(gx, sumMax) : sumMax;
                // Sub-groups of variant models (for example "Special Weapons") are counted inside the parent group's total.
                foreach (var sub in Children(child).Where(c => IsGroup(c)))
                    AddNested(sub, models, unitName, child, gmn, gmx);
            }
        }
    }

    private void AddNested(JsonObject group, List<ModelComposition> models, string unitName, JsonObject parent, int? parentMin, int? parentMax)
    {
        var (_, gmx) = Limits(group);
        foreach (var child in Children(group))
        {
            if (BsdDatabase.Str(child, "type") == "model")
            {
                var (mn, mx) = Limits(child);
                var hi = Math.Min(mx ?? gmx ?? 0, gmx ?? int.MaxValue);
                var composed = Compose(child, BsdDatabase.Str(child, "name") ?? unitName, mn ?? 0, hi);
                models.Add(composed);
                _members[composed] = new Member(parent, group, parentMin, parentMax, gmx, false);
            }
            else if (IsGroup(child)) AddNested(child, models, unitName, parent, parentMin, parentMax);
        }
    }

    /// <summary>The "Unit" profile attached directly to the entry (its own profiles or linked shared profiles).</summary>
    private string? OwnUnitProfileName(JsonObject entry)
    {
        var own = BsdDatabase.Arr(entry, "profiles").FirstOrDefault(p => BsdDatabase.Str(p, "typeName") == "Unit");
        if (own is not null) return BsdDatabase.Str(own, "name");
        foreach (var l in BsdDatabase.Arr(entry, "infoLinks"))
            if (BsdDatabase.Str(l, "type") == "profile" && BsdDatabase.Str(l, "targetId") is { } id && db.ById.TryGetValue(id, out var t)
                && BsdDatabase.Str(t.Node, "typeName") == "Unit")
                return BsdDatabase.Str(t.Node, "name");
        return null;
    }

    private ModelComposition Compose(JsonObject model, string modelName, int min, int max)
    {
        var statline = _walker.Profiles(model, "Unit").Select(f => BsdDatabase.Str(f.Profile, "name")).FirstOrDefault(n => n is not null);
        var mc = new ModelComposition
        {
            ModelName = modelName.Trim(),
            StatlineRef = statline ?? _fallbackStatline,   // variants without a profile of their own use the unit-level one
            Limit = new ModelCountLimits { Min = min, Max = max },
        };
        _sources[mc] = model;
        return mc;
    }

    /// <summary>Direct child selection entries, groups and linked entries (not descending into groups).</summary>
    private IEnumerable<JsonObject> Children(JsonObject node)
    {
        foreach (var key in new[] { "selectionEntries", "selectionEntryGroups" })
            foreach (var c in BsdDatabase.Arr(node, key))
                if (!IsHidden(c)) yield return c;
        foreach (var l in BsdDatabase.Arr(node, "entryLinks"))
            if (BsdDatabase.Str(l, "targetId") is { } id && db.ById.TryGetValue(id, out var t) && !IsHidden(t.Node) && !IsHidden(l))
                yield return t.Node;
    }

    private static bool IsGroup(JsonObject n) => BsdDatabase.Str(n, "type") is null or "selectionEntryGroup";

    private static bool IsHidden(JsonObject n) => n["hidden"] is JsonValue v && v.TryGetValue<bool>(out var h) && h;

    /// <summary>Min/max "selections" constraints of an entry (the ones counted among its siblings).</summary>
    private static (int? Min, int? Max) Limits(JsonObject entry)
    {
        int? min = null, max = null;
        foreach (var c in BsdDatabase.Arr(entry, "constraints"))
        {
            if (BsdDatabase.Str(c, "field") != "selections" || BsdDatabase.Str(c, "scope") != "parent") continue;
            if (c["value"] is not JsonValue v || !v.TryGetValue<double>(out var d)) continue;
            switch (BsdDatabase.Str(c, "type"))
            {
                case "min": min = Math.Max(min ?? 0, (int)d); break;
                case "max": max = Math.Min(max ?? int.MaxValue, (int)d); break;
            }
        }
        return (min, max);
    }
}
