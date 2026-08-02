using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

public static partial class MediaLinkUtils
{
  // The ids a "![caption](media:id)" reference could ever target, so callers can decide what's worth
  // resolving before any rendering happens.
  public static IReadOnlySet<int> ExtractReferencedIds(string? markdown) =>
    string.IsNullOrEmpty(markdown)
      ? ImmutableHashSet<int>.Empty
      : MediaLinkPattern().Matches(markdown).Select(match => int.Parse(match.Groups[1].Value)).ToHashSet();

  [GeneratedRegex("media:(\\d+)")]
  private static partial Regex MediaLinkPattern();
}
