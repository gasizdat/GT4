using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

public static partial class MediaLinkUtils
{
  [GeneratedRegex("src=\"media:(\\d+)\"")]
  private static partial Regex MediaSrcPattern();

  // Rewrites the <img src="media:<id>"> destinations Markdig emits for a "![caption](media:id)" reference
  // into inline data URIs. An id absent from mediaSources (a dangling reference, or one belonging to
  // another person) is left as-is, so the image is simply left broken instead of throwing.
  public static string RewriteMediaSources(string html, IReadOnlyDictionary<int, string> mediaSources)
  {
    if (mediaSources.Count == 0)
      return html;

    return MediaSrcPattern().Replace(html, match =>
      mediaSources.TryGetValue(int.Parse(match.Groups[1].Value), out var dataUri) ? $"src=\"{dataUri}\"" : match.Value);
  }
}
