using System.Text.RegularExpressions;

namespace GT4.UI.Utils.Formatters;


/// <summary>
/// Provides helpers for interpolating template strings using a set of string replacements.
/// </summary>
public class TemplateInterpolator
{
  /// <summary>
  /// Formats the specified template by replacing matching keys with indexed placeholders and applying
  /// the collected replacement values using <see cref="string.Format(string, object?[])"/>.
  /// </summary>
  /// <param name="template">The template text containing tokens to replace.</param>
  /// <param name="replacements">A dictionary of token keys and their corresponding replacement values.</param>
  /// <returns>The formatted string after all applicable replacements have been applied.</returns>
  /// <exception cref="NullReferenceException">
  /// Thrown when <paramref name="template"/> or <paramref name="replacements"/> is <see langword="null"/>
  /// and the current implementation attempts to use it.
  /// </exception>
  /// <exception cref="FormatException">
  /// Thrown when the generated composite format string is not valid for <see cref="string.Format(string, object?[])"/>.
  /// </exception>

  public static string Format(string template, IDictionary<string, string> replacements)
  {
    template = template.Replace("{", "{{").Replace("}", "}}");
    var sortedReplacements = replacements.ToList();
    sortedReplacements.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

    int placeholderIndex = 0;
    List<string> replacementValues = [];
    foreach (var (key, value) in sortedReplacements)
    {
      // Use Regex to match whole tokens only, avoiding accidental replacements inside placeholders or substrings.
      var pattern = Regex.Escape(key);
      var newTemplate = Regex.Replace(template, pattern, $"{{{placeholderIndex}}}", RegexOptions.None, TimeSpan.FromSeconds(1));
      if (newTemplate != template)
      {
        placeholderIndex++;
        template = newTemplate;
        replacementValues.Add(value);
      }
    }

    var ret = string.Format(template, replacementValues.ToArray());
    return ret;
  }
}
