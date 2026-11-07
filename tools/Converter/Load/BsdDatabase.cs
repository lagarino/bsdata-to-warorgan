using System.Text.Json;
using System.Text.Json.Nodes;

namespace WarOrganConverter.Load;

/// <summary>All BSData catalogues of one checkout, with an id index (first definition wins, as in the analysis scripts).</summary>
public sealed class BsdDatabase
{
    public Dictionary<string, JsonObject> Roots { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, (JsonObject Node, string File)> ById { get; } = new(StringComparer.Ordinal);

    public static BsdDatabase Load(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"BSData folder not found: {directory}");
        var db = new BsdDatabase();
        foreach (var path in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var doc = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            var root = (doc?["catalogue"] ?? doc?["gameSystem"]) as JsonObject;
            if (root is null) continue;
            var file = Path.GetFileName(path);
            db.Roots[file] = root;
            db.Index(root, file);
        }
        if (db.Roots.Count == 0)
            throw new InvalidOperationException($"No BSData catalogues found in {directory}");
        return db;
    }

    private void Index(JsonNode? n, string file)
    {
        switch (n)
        {
            case JsonObject o:
                if (o["id"] is JsonValue v && v.TryGetValue<string>(out var id))
                    ById.TryAdd(id, (o, file));
                foreach (var kv in o) Index(kv.Value, file);
                break;
            case JsonArray a:
                foreach (var x in a) Index(x, file);
                break;
        }
    }

    public string? Name(string? id) => id is not null && ById.TryGetValue(id, out var e) ? Str(e.Node, "name") : id;

    public static string? Str(JsonNode? n, string key) => n?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public static IEnumerable<JsonObject> Arr(JsonNode? n, string key) =>
        n?[key] is JsonArray a ? a.OfType<JsonObject>() : Enumerable.Empty<JsonObject>();
}
