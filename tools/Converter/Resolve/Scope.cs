namespace WarOrganConverter.Resolve;

/// <summary>Scope decisions D6, D8, D9 (docs/00-scope-and-decisions.md).</summary>
public static class Scope
{
    /// <summary>Files that are not converted at all (Unaligned Forces, Legends library).</summary>
    public static bool IsExcludedFile(string file) =>
        file.Contains("Unaligned Forces", StringComparison.OrdinalIgnoreCase) ||
        file.Contains("Legends", StringComparison.OrdinalIgnoreCase);

    /// <summary>Legends and Crucible entries are recognised by their name suffix.</summary>
    public static bool IsExcludedUnitName(string? name) =>
        name is not null && (name.Contains("[Legends]", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("[Crucible]", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Subtrees below a unit that are not datasheet content: Crusade, enhancements, weapon modifications, warlord
    /// selection and detachment or Boarding Actions options.
    /// </summary>
    public static bool IsSkippedEntry(string? name) =>
        name is not null && SkippedPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase) && (name.Length == p.Length || !char.IsLetter(name[p.Length])));

    private static readonly string[] SkippedPrefixes =
        ["Crusade", "Weapon Modifications", "Warlord", "Enhancements", "Detachment", "Boarding"];
}
