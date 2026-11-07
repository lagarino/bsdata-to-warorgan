using System.Text.Json.Nodes;
using WarOrganConverter.Load;

namespace WarOrganConverter.Resolve;

/// <summary>
/// Evaluates "hidden" modifiers for a roster with nothing selected: conditions on the primary catalogue are decided by the catalogue
/// being converted, conditions on selections or associations hold as if nothing were selected, anything else (roster toggles) is unknown.
/// </summary>
public static class Visibility
{
    /// <summary>Whether the modifiers of the nodes hide the entry (the first node's own flag is the starting state).</summary>
    public static bool IsHidden(JsonObject link, JsonObject target, string? primaryCatalogueId)
    {
        var hidden = link["hidden"] is JsonValue h && h.TryGetValue<bool>(out var b) && b;
        foreach (var node in new[] { link, target })
            foreach (var m in BsdDatabase.Arr(node, "modifiers"))
            {
                if (BsdDatabase.Str(m, "field") != "hidden" || m["value"] is not JsonValue v || !v.TryGetValue<bool>(out var value)) continue;
                if (m["conditions"] is null && m["conditionGroups"] is null) continue;
                if (HoldsWithNothingSelected(m, primaryCatalogueId) == true) hidden = value;
            }
        return hidden;
    }

    /// <summary>Whether the conditions of a node hold when nothing is selected; null when they cannot be told.</summary>
    public static bool? HoldsWithNothingSelected(JsonObject node, string? primaryCatalogueId)
    {
        var results = new List<bool?>();
        foreach (var c in BsdDatabase.Arr(node, "conditions"))
        {
            var type = BsdDatabase.Str(c, "type");
            var field = BsdDatabase.Str(c, "field");
            var scope = BsdDatabase.Str(c, "scope");
            var value = c["value"] is JsonValue v && v.TryGetValue<double>(out var d) ? d : (double?)null;
            if (scope == "primary-catalogue" && field == "selections" && primaryCatalogueId is not null)
            {
                var isPrimary = BsdDatabase.Str(c, "childId") == primaryCatalogueId;
                results.Add(type switch { "instanceOf" => isPrimary, "notInstanceOf" => !isPrimary, _ => null });
                continue;
            }
            if (scope is "roster" or "primary-catalogue" || field is not ("selections" or "associations") || value is null) { results.Add(null); continue; }
            results.Add(type switch
            {
                "lessThan" => 0 < value,
                "atMost" => 0 <= value,
                "equalTo" => value == 0,
                "greaterThan" => 0 > value,
                "atLeast" => 0 >= value,
                "notEqualTo" => value != 0,
                "notInstanceOf" => true,
                "instanceOf" => false,
                _ => null,
            });
        }
        foreach (var g in BsdDatabase.Arr(node, "conditionGroups")) results.Add(HoldsWithNothingSelected(g, primaryCatalogueId));
        if (results.Count == 0) return null;
        if (BsdDatabase.Str(node, "type") == "or") return results.Any(r => r == true) ? true : results.Any(r => r is null) ? null : false;
        return results.Any(r => r == false) ? false : results.Any(r => r is null) ? null : true;
    }
}
