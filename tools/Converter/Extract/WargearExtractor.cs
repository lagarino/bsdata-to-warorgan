using System.Text.Json.Nodes;
using WarOrgan.Model.Unit;
using WarOrgan.Model.Unit.Wargear;
using WarOrganConverter.Load;
using WarOrganConverter.Resolve;

namespace WarOrganConverter.Extract;

/// <summary>
/// Starting wargear and options of each model composition (docs/03-mapping.md, wargear).
/// BSData 11e mostly encodes loadouts as separate model variants, so the common case is: required upgrades
/// (min 1) are the model's InitialWargear, an exactly-one group is a replacement choice, optional upgrades and
/// optional groups are additions. Upgrades that cost points or grant an ability become WargearDefinitions.
/// Conditional visibility (hidden modifiers) is not evaluated; this is the baseline view.
/// </summary>
public sealed class WargearExtractor(BsdDatabase db)
{
    private readonly EntryWalker _walker = new(db);

    private sealed record Node(JsonObject Entry, int? Min, int? Max, string? LinkId = null);

    public void Apply(Unit unit, JsonObject unitEntry, CompositionExtractor.Result comp, List<string> warnings)
    {
        var unitName = BsdDatabase.Str(unitEntry, "name") ?? "?";
        foreach (var model in comp.Models)
        {
            if (!comp.Sources.TryGetValue(model, out var source)) continue;
            var gear = new ModelWargear();
            Map(source, gear, unit, unitName, warnings);
            // Unit-level gear (children of the unit entry that are not models) belongs to the only model.
            if (comp.Models.Count == 1 && !ReferenceEquals(source, unitEntry))
                Map(unitEntry, gear, unit, unitName, warnings, skipModels: true);
            if (gear.InitialWargear.Count > 0 || gear.Options.Count > 0) model.Wargear.Add(gear);
        }

        // Gear offered by the unit as a whole ("Instrument of Chaos", "Icon") when it has several kinds of model goes to the kind with the
        // most models: a leader or sergeant of one is not the model that carries it.
        if (comp.Models.Count > 1)
        {
            var unitGear = new ModelWargear();
            Map(unitEntry, unitGear, unit, unitName, warnings, skipModels: true);
            if (unitGear.InitialWargear.Count > 0 || unitGear.Options.Count > 0)
            {
                var carrier = comp.Models.OrderByDescending(m => m.Limit?.Max ?? 0).First();
                var target = carrier.Wargear.FirstOrDefault() ?? new ModelWargear();
                if (carrier.Wargear.Count == 0) carrier.Wargear.Add(target);
                foreach (var item in unitGear.InitialWargear.Where(i => !target.InitialWargear.Contains(i))) target.InitialWargear.Add(item);
                target.Options.AddRange(unitGear.Options);
            }
        }

        // Weapons nobody can select would fail validation; say so.
        var selectable = comp.Models.SelectMany(m => m.Wargear)
            .SelectMany(w => w.InitialWargear.Concat(w.Options.SelectMany(o => o.Options).SelectMany(c => c.Items)))
            .ToHashSet(StringComparer.Ordinal);
        // A weapon no path of options reaches would be unselectable (the builder rejects that): offer it as an optional addition to the
        // model whose entry contains it, and say so.
        foreach (var id in unit.Weapons.SelectMany(w => w.Weapons).Select(w => w.Id!).Distinct().Where(id => !selectable.Contains(id)).ToList())
        {
            var owner = comp.Models.FirstOrDefault(m => comp.Sources.TryGetValue(m, out var src)
                    && _walker.Profiles(src, "Ranged Weapons", "Melee Weapons").Any(f => (BsdDatabase.Str(f.Owner, "name") ?? BsdDatabase.Str(f.Profile, "name") ?? "").Trim().ToLowerInvariant() == id))
                ?? comp.Models.FirstOrDefault();
            if (owner is null) continue;
            var gear = owner.Wargear.FirstOrDefault() ?? new ModelWargear();
            if (owner.Wargear.Count == 0) owner.Wargear.Add(gear);
            gear.Options.Add(new WargearOption { Options = [new WargearChoice { Items = [id] }] });
            warnings.Add($"{unitName}: weapon '{id}' is not reached by the datasheet's options; offered as an optional addition to '{owner.ModelName}'");
        }
    }

    private void Map(JsonObject container, ModelWargear gear, Unit unit, string unitName, List<string> warnings, bool skipModels = false)
    {
        foreach (var node in Children(container, skipModels))
        {
            var e = node.Entry;
            if (IsGroup(e) || (IsGear(e) && !HasOwnGear(e) && HasChildren(e)))
            {
                var items = Children(e, true).ToList();
                var gmin = node.Min ?? 0;
                var gmax = node.Max;
                if (gmin == 0 && gmax is null)
                {
                    // A plain container (for example "Wargear"): treat its children as direct.
                    Map(e, gear, unit, unitName, warnings, true);
                    continue;
                }
                MapGroup(e, node.LinkId, items, gmin, gmax, gear, unit, unitName, warnings);
            }
            else if (IsGear(e))
            {
                var key = Register(e, unit, unitName, warnings);
                if (key is null) continue;
                if ((node.Min ?? 0) >= 1) { if (!gear.InitialWargear.Contains(key)) gear.InitialWargear.Add(key); }
                else gear.Options.Add(new WargearOption { Max = node.Max is > 0 ? node.Max : null, Options = [new WargearChoice { Items = [key] }] });
            }
        }
    }

    /// <summary>The wargear keys an item stands for: its own key, or for a wrapper entry the required keys of its children.</summary>
    private List<string> KeysOf(Node item, ModelWargear gear, Unit unit, string unitName, List<string> warnings)
    {
        var e = item.Entry;
        if (IsGear(e) && !HasOwnGear(e) && HasChildren(e))
        {
            // A wrapper such as "Choppa and Kombi-skorcha": its required parts are the choice's items; nested choices and
            // optional parts become options that apply once the wrapper is equipped.
            var inner = new ModelWargear();
            Map(e, inner, unit, unitName, warnings, true);
            gear.Options.AddRange(inner.Options);
            return inner.InitialWargear;
        }
        return IsGear(e) && Register(e, unit, unitName, warnings) is { } key ? [key] : [];
    }

    private void MapGroup(JsonObject group, string? groupLinkId, List<Node> items, int gmin, int? gmax, ModelWargear gear, Unit unit, string unitName, List<string> warnings)
    {
        // Sub-groups ("Support Systems" containing "Secondary Weapons") are mapped on their own limits.
        foreach (var sub in items.Where(i => IsGroup(i.Entry)))
        {
            if ((sub.Min ?? 0) == 0 && sub.Max is null) Map(sub.Entry, gear, unit, unitName, warnings, true);
            else MapGroup(sub.Entry, sub.LinkId, Children(sub.Entry, true).ToList(), sub.Min ?? 0, sub.Max, gear, unit, unitName, warnings);
        }
        items = items.Where(i => !IsGroup(i.Entry)).ToList();
        var keyed = items.Select(i => (Node: i, Keys: KeysOf(i, gear, unit, unitName, warnings))).Where(x => x.Keys.Count > 0).ToList();
        if (keyed.Count == 0) return;
        var groupName = BsdDatabase.Str(group, "name");
        if (gmin >= 1 && gmax == 1)
        {
            var defId = BsdDatabase.Str(group, "defaultSelectionEntryId");
            var def = keyed.FirstOrDefault(k => defId is not null && (BsdDatabase.Str(k.Node.Entry, "id") == defId || k.Node.LinkId == defId));
            if (def.Keys is null) def = keyed[0];
            foreach (var k in def.Keys)
                if (!gear.InitialWargear.Contains(k)) gear.InitialWargear.Add(k);
            var others = keyed.Where(k => !ReferenceEquals(k.Keys, def.Keys)).Select(k => new WargearChoice { Items = k.Keys }).ToList();
            if (others.Count > 0) gear.Options.Add(new WargearOption { Replaces = def.Keys.ToList(), Options = others });
        }
        else if (gmin == 0 || gmax is > 1)
            gear.Options.Add(new WargearOption { Max = gmax is > 0 ? gmax : null, Options = keyed.Select(k => new WargearChoice { Items = k.Keys }).ToList() });
        else warnings.Add($"{unitName}: wargear group '{groupName}' with min {gmin} max {gmax?.ToString() ?? "-"} not mapped");
    }

    private static bool HasChildren(JsonObject e) =>
        e["selectionEntries"] is JsonArray { Count: > 0 } || e["selectionEntryGroups"] is JsonArray { Count: > 0 } || e["entryLinks"] is JsonArray { Count: > 0 };

    private bool HasOwnGear(JsonObject e) =>
        BsdDatabase.Arr(e, "profiles").Any(p => BsdDatabase.Str(p, "typeName") is "Ranged Weapons" or "Melee Weapons" or "Abilities")
        || BsdDatabase.Arr(e, "costs").Any(c => BsdDatabase.Str(c, "typeId") == PointsExtractor.PtsTypeId && c["value"] is JsonValue v && v.TryGetValue<double>(out var d) && d > 0);

    /// <summary>Creates the weapon/definition entries an upgrade needs and returns its wargear key.</summary>
    private string? Register(JsonObject e, Unit unit, string unitName, List<string> warnings)
    {
        var name = BsdDatabase.Str(e, "name")?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        var key = name.ToLowerInvariant();
        var cost = BsdDatabase.Arr(e, "costs").Where(c => BsdDatabase.Str(c, "typeId") == PointsExtractor.PtsTypeId)
            .Select(c => c["value"] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0).FirstOrDefault();
        var abilities = BsdDatabase.Arr(e, "profiles").Where(p => BsdDatabase.Str(p, "typeName") == "Abilities").ToList();
        var hasWeapon = BsdDatabase.Arr(e, "profiles").Any(p => BsdDatabase.Str(p, "typeName") is "Ranged Weapons" or "Melee Weapons")
            || BsdDatabase.Arr(e, "infoLinks").Any(l => BsdDatabase.Str(l, "type") == "profile" && db.ById.TryGetValue(BsdDatabase.Str(l, "targetId") ?? "", out var t)
                                                        && BsdDatabase.Str(t.Node, "typeName") is "Ranged Weapons" or "Melee Weapons");
        if (cost <= 0 && abilities.Count == 0)
            return hasWeapon ? key : null; // plain weapon: its key is the weapon id, no definition needed

        var def = unit.UnitComposition.WargearDefinitions.FirstOrDefault(d => d.Key == key);
        if (def is null)
        {
            def = new WargearDefinition { Key = key, DisplayName = name };
            unit.UnitComposition.WargearDefinitions.Add(def);
        }
        if (cost > 0) def.Cost = cost;
        foreach (var p in abilities)
        {
            var title = BsdDatabase.Str(p, "name")?.Trim();
            var text = BsdDatabase.Arr(p, "characteristics").Select(c => BsdDatabase.Str(c, "$text")).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (string.IsNullOrEmpty(title) || string.IsNullOrWhiteSpace(text)) continue;
            if (!unit.UnitAbilities.Any(a => a.Title == title))
                unit.UnitAbilities.Add(new UnitInfoSection { Title = title, Text = text.Trim() });
            (def.GrantsAbilities ??= []).Add(title);
        }
        return key;
    }

    private bool IsGear(JsonObject e) =>
        BsdDatabase.Str(e, "type") is "upgrade" or "selectionEntry" or null && !IsGroup(e) && BsdDatabase.Str(e, "type") != "model";

    private static bool IsGroup(JsonObject e) => BsdDatabase.Str(e, "type") is null && (e["selectionEntries"] is not null || e["entryLinks"] is not null || e["selectionEntryGroups"] is not null);

    private IEnumerable<Node> Children(JsonObject container, bool skipModels)
    {
        foreach (var key in new[] { "selectionEntries", "selectionEntryGroups" })
            foreach (var c in BsdDatabase.Arr(container, key))
                if (Include(c, skipModels)) yield return new Node(c, Limits(c).Min, Limits(c).Max);
        foreach (var l in BsdDatabase.Arr(container, "entryLinks"))
        {
            if (BsdDatabase.Str(l, "targetId") is not { } id || !db.ById.TryGetValue(id, out var t)) continue;
            if (!Include(t.Node, skipModels) || IsHidden(l)) continue;
            var (tmin, tmax) = Limits(t.Node);
            var (lmin, lmax) = Limits(l);
            yield return new Node(t.Node, Max(tmin, lmin), Min(tmax, lmax), BsdDatabase.Str(l, "id"));
        }
    }

    private static int? Max(int? a, int? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
    private static int? Min(int? a, int? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private bool Include(JsonObject c, bool skipModels)
    {
        if (IsHidden(c)) return false;
        if (skipModels && BsdDatabase.Str(c, "type") == "model") return false;
        return !Scope.IsSkippedEntry(BsdDatabase.Str(c, "name"));
    }

    private static bool IsHidden(JsonObject n) => n["hidden"] is JsonValue v && v.TryGetValue<bool>(out var h) && h;

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
