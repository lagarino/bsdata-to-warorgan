using System.Text.RegularExpressions;
using WarOrgan.Model.DataSet;

namespace WarOrganConverter.Emit;

/// <summary>
/// Builds the core-ability and weapon-ability regex lists the app uses to look up descriptions
/// (docs/02-target-model.md). Every ability a unit or weapon uses needs a regex that matches it.
/// </summary>
public static class AbilityDefinitions
{
    public const string MissingDescription = "(No description was found in the source data.)";

    /// <summary>Weapon keywords: unify the Unicode hyphens, split on commas, drop empty and "-" placeholders.</summary>
    public static IEnumerable<string> SplitKeywords(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords)) yield break;
        var text = keywords.Replace('‑', '-').Replace('‐', '-').Replace('–', '-').Replace(' ', ' ');
        foreach (var part in text.Split(','))
        {
            var k = Regex.Replace(part.Trim(), @"\s+", " ");
            if (k.Length > 0 && k != "-") yield return k;
        }
    }

    public static string NormaliseKeywords(string? keywords)
    {
        var list = SplitKeywords(keywords).ToList();
        return list.Count == 0 ? "" : string.Join(", ", list);
    }

    /// <summary>
    /// One regex per source rule the tokens refer to. A token refers to a rule when it equals the rule's name or starts
    /// with it followed by a space, hyphen or colon ("Anti-Infantry 4+" -> "Anti"); the shortest such rule name wins.
    /// Tokens no rule covers get an exact-match regex with a placeholder description and are listed in <paramref name="unmatched"/>.
    /// </summary>
    public static List<RegAbility> Build(IEnumerable<string> tokens, IReadOnlyDictionary<string, string> rules, List<string> unmatched)
    {
        var result = new List<RegAbility>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens.Select(t => t.Trim('[', ']').Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            var rule = rules.Keys
                .Where(name => Covers(name, token))
                .OrderBy(name => name.Length).ThenBy(name => name, StringComparer.Ordinal)
                .FirstOrDefault();
            string regex, description;
            if (rule is null)
            {
                unmatched.Add(token);
                regex = "^" + Regex.Escape(token) + "$";
                description = MissingDescription;
            }
            else
            {
                regex = "^" + Regex.Escape(rule) + @"(?:[\ \-:].*)?$";
                description = string.IsNullOrWhiteSpace(rules[rule]) ? MissingDescription : rules[rule].Trim();
            }
            if (seen.Add(regex)) result.Add(new RegAbility { Regex = regex, Description = description });
        }
        return result;
    }

    private static bool Covers(string ruleName, string token)
    {
        if (token.Equals(ruleName, StringComparison.OrdinalIgnoreCase)) return true;
        return token.StartsWith(ruleName, StringComparison.OrdinalIgnoreCase)
               && token.Length > ruleName.Length && " -:".Contains(token[ruleName.Length]);
    }
}
