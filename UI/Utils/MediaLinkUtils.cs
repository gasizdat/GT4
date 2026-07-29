using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

public static partial class MediaLinkUtils
{
  [GeneratedRegex("src=\"media:(\\d+)\"")]
  private static partial Regex MediaSrcPattern();

  [GeneratedRegex("media:(\\d+)")]
  private static partial Regex MediaLinkPattern();

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

  // The ids a "![caption](media:id)" reference could ever target, read off the raw markdown (not the
  // rendered HTML) so callers can decide what's worth encoding before any rendering happens.
  public static IReadOnlySet<int> ExtractReferencedIds(string? markdown) =>
    string.IsNullOrEmpty(markdown)
      ? ImmutableHashSet<int>.Empty
      : MediaLinkPattern().Matches(markdown).Select(match => int.Parse(match.Groups[1].Value)).ToHashSet();
}
