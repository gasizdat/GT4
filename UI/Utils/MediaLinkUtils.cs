using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

public static partial class MediaLinkUtils
{
  // "person:"/"attachment:"/"media:" are custom opaque schemes (like "mailto:"). Uri parses them --
  // scheme plus opaque payload -- but its hierarchical members (Host, AbsolutePath) aren't meaningful
  // for them, so the id is taken directly from the string.
  public static bool TryParseLinkId(string url, string scheme, out int id)
  {
    id = 0;
    return url.StartsWith(scheme, StringComparison.Ordinal) && int.TryParse(url.AsSpan(scheme.Length), out id);
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

  // The alternation is the whole range check: 100-299, or 1-99 with no leading zero; anything else stays caption.
  [GeneratedRegex(@"(?:^|\s)([1-2][0-9]{2}|[1-9][0-9]?)%$")]
  private static partial Regex ImageWidthPattern();
}
