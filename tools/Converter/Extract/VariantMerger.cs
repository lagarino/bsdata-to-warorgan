using WarOrgan.Data.BuildDataset.Utils;
using WarOrgan.Model.Unit;
using WarOrgan.Model.Unit.Wargear;

namespace WarOrganConverter.Extract;

/// <summary>
/// BSData lists each loadout of a model as its own model entry and caps the total in the surrounding group
/// (for example "10-20 Warriors" containing "w/ flayer" and "w/ reaper", each allowed up to 20). WarOrgan limits
/// every model entry on its own, so copying the variants one-to-one would let a unit grow far beyond its group cap.
/// Variants with the same statline in the same group are therefore merged into one model entry that carries the
/// group's limits, and the other loadouts become replacement or addition options on it.
/// </summary>
public static class VariantMerger
{
    public static void Merge(Unit unit, CompositionExtractor.Result comp, List<string> warnings)
    {
        var unitName = unit.DisplayName ?? unit.Id;
        var buckets = comp.Models
            .Where(m => comp.Members.TryGetValue(m, out var info) && info.Group is not null)
            .GroupBy(m => (comp.Members[m].Group, m.StatlineRef))
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var bucket in buckets)
        {
            var members = bucket.ToList();
            var info = comp.Members[members[0]];
            var direct = members.Where(m => comp.Members[m].SubGroup is null).ToList();
            var baseModel = direct.FirstOrDefault(m => comp.Members[m].IsDefault) ?? direct.FirstOrDefault() ?? members[0];
            var variants = members.Where(m => !ReferenceEquals(m, baseModel)).ToList();
            var wholeGroup = comp.Models.Count(m => comp.Members.TryGetValue(m, out var i) && ReferenceEquals(i.Group, info.Group)) == members.Count;

            // The group's total limit applies to every variant together.
            var min = wholeGroup ? info.GroupMin ?? members.Sum(m => m.Limit!.Min) : members.Sum(m => m.Limit!.Min);
            var max = wholeGroup ? info.GroupMax ?? members.Sum(m => m.Limit!.Max) : Math.Min(members.Sum(m => m.Limit!.Max), info.GroupMax ?? int.MaxValue);
            var baseGear = baseModel.Wargear.FirstOrDefault() ?? new ModelWargear();
            if (baseModel.Wargear.Count == 0) baseModel.Wargear.Add(baseGear);

            var options = new Dictionary<(string Replaced, object? Sub), (WargearOption Option, int Cap, bool Unlimited)>();
            foreach (var variant in variants)
            {
                var variantGear = variant.Wargear.FirstOrDefault();
                var replaced = Subtract(baseGear.InitialWargear, variantGear?.InitialWargear ?? []);
                var added = Subtract(variantGear?.InitialWargear ?? [], baseGear.InitialWargear);
                var vinfo = comp.Members[variant];
                if (added.Count > 0 || replaced.Count > 0)
                {
                    var key = (string.Join("|", replaced.Order(StringComparer.Ordinal)), (object?)vinfo.SubGroup);
                    if (!options.TryGetValue(key, out var entry))
                        options[key] = entry = (new WargearOption { Replaces = replaced.Count > 0 ? replaced : null }, 0, false);
                    if (!entry.Option.Options.Any(c => c.Items.SequenceEqual(added)))
                        entry.Option.Options.Add(new WargearChoice { Items = added });
                    var cap = vinfo.SubGroupMax ?? variant.Limit!.Max;
                    // Variants that share a sub-group share its cap; separate variants add up.
                    var total = vinfo.SubGroup is not null ? vinfo.SubGroupMax ?? cap : entry.Cap + cap;
                    options[key] = (entry.Option, total, entry.Unlimited || cap >= max);
                }
                // Options the variant brings itself (for example a nested choice) carry over when they start from items the base has.
                foreach (var opt in variantGear?.Options ?? [])
                    if ((opt.Replaces ?? []).All(baseGear.InitialWargear.Contains) && !baseGear.Options.Any(o => o.AreEqual(opt)))
                        baseGear.Options.Add(opt);
                    else if (!(opt.Replaces ?? []).All(baseGear.InitialWargear.Contains))
                        warnings.Add($"{unitName}: option of '{variant.ModelName}' replaces gear the merged model does not start with; dropped");
            }
            foreach (var (option, cap, unlimited) in options.Values)
            {
                option.Max = unlimited || cap >= max ? null : cap;
                baseGear.Options.Add(option);
            }

            baseModel.Limit = new ModelCountLimits { Min = min, Max = max };
            if (!string.IsNullOrWhiteSpace(baseModel.StatlineRef)) baseModel.ModelName = baseModel.StatlineRef!;
            foreach (var v in variants)
            {
                unit.UnitComposition.ModelCompositions.Remove(v);
                comp.Models.Remove(v);
            }
        }

        MakeNamesUnique(unit, warnings, unitName);
    }

    private static void MakeNamesUnique(Unit unit, List<string> warnings, string unitName)
    {
        foreach (var dup in unit.UnitComposition.ModelCompositions.GroupBy(m => m.ModelName).Where(g => g.Count() > 1).ToList())
        {
            var i = 1;
            foreach (var m in dup.Skip(1)) m.ModelName = $"{dup.Key} ({++i})";
            warnings.Add($"{unitName}: model name '{dup.Key}' appears {dup.Count()} times; renamed the later ones");
        }
    }

    /// <summary>Items of <paramref name="from"/> that are not in <paramref name="remove"/>, counting repeated items.</summary>
    private static List<string> Subtract(IEnumerable<string> from, IEnumerable<string> remove)
    {
        var result = from.ToList();
        foreach (var r in remove) result.Remove(r);
        return result;
    }
}
