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

  // A trailing percentage in an image's description -- "![Grandpa 50%](media:5)" -- asks for that share
  // of the available width, and the rest of the description stays the caption. Any description ending
  // in 1-100% reads as a size, so "Sale 50%" sizes the image rather than captioning it.
  public static (int? WidthPercent, string Caption) ParseImageDescription(string description)
  {
    var match = ImageWidthPattern().Match(description);
    if (!match.Success)
    {
      return (null, description);
    }

    return (int.Parse(match.Groups[1].Value), description[..match.Index]);
  }

  [GeneratedRegex("media:(\\d+)")]
  private static partial Regex MediaLinkPattern();

  // The alternation is the whole range check: 100, or 1-99 with no leading zero; anything else stays caption.
  [GeneratedRegex(@"(?:^|\s)(100|[1-9][0-9]?)%$")]
  private static partial Regex ImageWidthPattern();
}
