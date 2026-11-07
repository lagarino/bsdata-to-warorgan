using System.Text.Json.Nodes;
using WarOrganConverter.Load;

namespace WarOrganConverter.Resolve;

public sealed record ResolvedUnit(JsonObject Entry, string OwnerFile, bool Imported);

/// <summary>
/// Effective roster of a catalogue (docs/08 section 1): its own root entryLinks
/// and the root entries of every catalogue linked with importRootEntries (not transitive).
/// A unit is a root-linked entry of type unit or model.
/// </summary>
public sealed class RosterResolver(BsdDatabase db)
{
    public IReadOnlyList<ResolvedUnit> Resolve(string catalogueFile)
    {
        var result = new List<ResolvedUnit>();
        var seen = new HashSet<string>();
        var primaryId = BsdDatabase.Str(db.Roots[catalogueFile], "id");
        AddRootUnits(catalogueFile, imported: false, result, seen, primaryId);
        foreach (var l in BsdDatabase.Arr(db.Roots[catalogueFile], "catalogueLinks"))
        {
            if (l["importRootEntries"] is not JsonValue v || !v.TryGetValue<bool>(out var imp) || !imp) continue;
            var target = FileOfCatalogue(BsdDatabase.Str(l, "targetId"));
            if (target is null || Scope.IsExcludedFile(target)) continue;
            AddRootUnits(target, imported: true, result, seen, primaryId);
        }
        return result;
    }

    public string? FileOfCatalogue(string? id) =>
        db.Roots.Where(kv => BsdDatabase.Str(kv.Value, "id") == id).Select(kv => kv.Key).FirstOrDefault();

    private void AddRootUnits(string sourceFile, bool imported, List<ResolvedUnit> result, HashSet<string> seen, string? primaryId)
    {
        var root = db.Roots[sourceFile];
        // Only root-level entryLinks count: shared entries that nothing links at the root are not datasheets.
        foreach (var l in BsdDatabase.Arr(root, "entryLinks"))
        {
            var tid = BsdDatabase.Str(l, "targetId");
            if (tid is null || !db.ById.TryGetValue(tid, out var t)) continue;
            // Units the catalogue's own primary-catalogue rules hide (a chapter with its own version of a datasheet) are not in the roster.
            if (Visibility.IsHidden(l, t.Node, primaryId)) continue;
            Add(t.Node, t.File, imported, result, seen);
        }
    }

    private static void Add(JsonObject e, string ownerFile, bool imported, List<ResolvedUnit> result, HashSet<string> seen)
    {
        if (BsdDatabase.Str(e, "type") is not ("unit" or "model")) return;
        if (Scope.IsExcludedFile(ownerFile) || Scope.IsExcludedUnitName(BsdDatabase.Str(e, "name"))) return;
        if (!seen.Add(BsdDatabase.Str(e, "id") ?? Guid.NewGuid().ToString())) return;
        // A chapter lists its own version of a datasheet next to the parent's: the first one found (its own) is the one that counts.
        if (!seen.Add("name:" + (BsdDatabase.Str(e, "name") ?? "").Trim().ToLowerInvariant())) return;
        result.Add(new ResolvedUnit(e, ownerFile, imported));
    }
}
