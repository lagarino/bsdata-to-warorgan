using System.Text.Json.Nodes;
using WarOrganConverter.Load;

namespace WarOrganConverter.Resolve;

/// <summary>
/// Walks the selection-entry tree below a unit, following entryLinks (by target id) and infoLinks to shared profiles.
/// Conditional modifiers are not interpreted here (a baseline "everything visible" view); static hidden entries are skipped.
/// </summary>
public sealed class EntryWalker(BsdDatabase db)
{
    public sealed record Found(JsonObject Profile, JsonObject Owner);

    /// <summary>All profiles of the given type name below (and including) the entry, with the entry that carries each one.</summary>
    public IEnumerable<Found> Profiles(JsonObject entry, params string[] typeNames)
    {
        var results = new List<Found>();
        Visit(entry, new HashSet<JsonObject>(ReferenceEqualityComparer.Instance), node =>
        {
            foreach (var p in BsdDatabase.Arr(node, "profiles"))
                if (typeNames.Contains(BsdDatabase.Str(p, "typeName")) && !IsHidden(p))
                    results.Add(new Found(p, node));
            foreach (var l in BsdDatabase.Arr(node, "infoLinks"))
            {
                if (BsdDatabase.Str(l, "type") != "profile") continue;
                var tid = BsdDatabase.Str(l, "targetId");
                if (tid is not null && db.ById.TryGetValue(tid, out var t) && !IsHidden(t.Node)
                    && typeNames.Contains(BsdDatabase.Str(t.Node, "typeName")))
                    results.Add(new Found(t.Node, node));
            }
        });
        return results;
    }

    /// <summary>All selection entries below (and including) the entry.</summary>
    public IEnumerable<JsonObject> Entries(JsonObject entry)
    {
        var results = new List<JsonObject>();
        Visit(entry, new HashSet<JsonObject>(ReferenceEqualityComparer.Instance), results.Add);
        return results;
    }

    private void Visit(JsonObject node, HashSet<JsonObject> path, Action<JsonObject> action)
    {
        if (!path.Add(node)) return;
        if (path.Count > 1 && Scope.IsSkippedEntry(BsdDatabase.Str(node, "name"))) { path.Remove(node); return; }
        action(node);
        foreach (var key in new[] { "selectionEntries", "selectionEntryGroups" })
            foreach (var child in BsdDatabase.Arr(node, key))
                if (!IsHidden(child)) Visit(child, path, action);
        foreach (var l in BsdDatabase.Arr(node, "entryLinks"))
        {
            var tid = BsdDatabase.Str(l, "targetId");
            if (tid is not null && db.ById.TryGetValue(tid, out var t) && !IsHidden(t.Node) && !IsHidden(l))
                Visit(t.Node, path, action);
        }
        path.Remove(node);
    }

    private static bool IsHidden(JsonObject n) => n["hidden"] is JsonValue v && v.TryGetValue<bool>(out var h) && h;
}
