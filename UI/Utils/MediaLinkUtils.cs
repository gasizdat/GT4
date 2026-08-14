using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

public static partial class MediaLinkUtils
{
  // The ids a "![caption](media:id)" reference could ever target, so callers can decide what's worth
  // resolving before any rendering happens.
  public static IReadOnlySet<int> ExtractReferencedIds(string? markdown)
  {
    if (string.IsNullOrEmpty(markdown))
    {
      return ImmutableHashSet<int>.Empty;
    }

    var matches = MediaLinkPattern().Matches(markdown);
    var ids = matches.Select(match => ParseId(match.Groups[1].Value)).OfType<int>();
    return ids.ToHashSet();
  }

  // A trailing percentage in an image's description -- "![Grandpa 50%](media:5)" -- asks for that share
  // of the width the image would otherwise take, and the rest of the description stays the caption. Any
  // description ending in 1-299% reads as a size, so "Sale 50%" sizes the image rather than captioning it.
  public static (int? WidthPercent, string Caption) ParseImageDescription(string description)
  {
    var match = ImageWidthPattern().Match(description);
    if (!match.Success)
    {
      return (null, description);
    }

    return (int.Parse(match.Groups[1].Value), description[..match.Index]);
  }

  // A run of digits too long for an int is prose the user typed, not a link this app ever inserted --
  // and MarkdownView reparses the whole biography on every keystroke, so a throw here would surface as
  // an editor that dies mid-word rather than as a missing image.
  private static int? ParseId(string value) => int.TryParse(value, out var id) ? id : null;

  [GeneratedRegex("media:(\\d+)")]
  private static partial Regex MediaLinkPattern();

  // The alternation is the whole range check: 100-299, or 1-99 with no leading zero; anything else stays caption.
  [GeneratedRegex(@"(?:^|\s)([1-2][0-9]{2}|[1-9][0-9]?)%$")]
  private static partial Regex ImageWidthPattern();
}
