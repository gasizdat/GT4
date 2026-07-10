using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

public static class WildcardMatcher
{
  // A pattern with no explicit wildcard is treated as an implicit substring search (e.g. "ann" behaves
  // like "*ann*"), matching the plain-text search UX users expect, while '*'/'?' still work literally
  // for anyone who types them.
  public static bool IsMatch(string input, string pattern)
  {
    if (string.IsNullOrEmpty(pattern))
    {
      return true;
    }

    var hasWildcard = pattern.Contains('*') || pattern.Contains('?');
    var effectivePattern = hasWildcard ? pattern : $"*{pattern}*";
    var regexPattern = Regex.Escape(effectivePattern)
      .Replace(@"\*", ".*")
      .Replace(@"\?", ".");

    return Regex.IsMatch(input, $"^{regexPattern}$", RegexOptions.IgnoreCase);
  }
}
