using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace WarOrganConverter.Emit;

/// <summary>
/// BSData texts carry a small markup ("^^" around keywords, "**" or "***" for emphasis) that the WarOrgan app shows literally.
/// The marks are removed from every text and description of the dataset; non-breaking spaces become normal spaces and characters the source lost (U+FFFD) are dropped.
/// </summary>
public static partial class TextCleaner
{
    [GeneratedRegex(@"\^\^|\*{2,}")]
    private static partial Regex Markup();

    public static string Clean(string text) => Markup().Replace(text, "").Replace(' ', ' ').Replace("�", "");

    /// <summary>Cleans every string property called Text, Description, Value or Keywords below the object.</summary>
    public static void CleanAll(object? root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(root, seen);
    }

    private static void Visit(object? node, HashSet<object> seen)
    {
        if (node is null or string || node.GetType().IsPrimitive || node.GetType().IsEnum || !seen.Add(node)) return;
        if (node is IEnumerable items and not IDictionary)
        {
            foreach (var item in items) Visit(item, seen);
            return;
        }
        if (node.GetType().Namespace?.StartsWith("WarOrgan", StringComparison.Ordinal) != true) return;
        foreach (var p in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0 || !p.CanRead) continue;
            if (p.PropertyType == typeof(string))
            {
                if (p.CanWrite && p.Name is "Text" or "Description" or "Value" or "Keywords" && p.GetValue(node) is string s) p.SetValue(node, Clean(s));
            }
            else if (!p.PropertyType.IsValueType) Visit(p.GetValue(node), seen);
        }
    }
}
