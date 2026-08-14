using System.Text.RegularExpressions;

namespace GT4.UI.Utils;

/// <summary>
/// The link targets a biography's own markup uses: "person:", "attachment:" and "media:" followed by an
/// element id. Both directions live here so a writer and its reader cannot drift apart -- the url a
/// link is built from is the url the tap handler and the media resolver parse back.
/// </summary>
public static partial class MarkdownLinkUtils
{
  // Opaque schemes, like "mailto:". Uri does parse them -- scheme plus opaque payload -- but its
  // hierarchical members (Host, AbsolutePath) are meaningless for them, so an id is taken from the
  // string directly rather than through Uri.
  private const string PersonScheme = "person:";
  private const string AttachmentScheme = "attachment:";
  private const string MediaScheme = "media:";

  public static string PersonUrl(int personId) => $"{PersonScheme}{personId}";

  public static string AttachmentUrl(int attachmentId) => $"{AttachmentScheme}{attachmentId}";

  public static string MediaUrl(int mediaId) => $"{MediaScheme}{mediaId}";

  public static bool TryParsePersonId(string url, out int personId) => TryParseId(url, PersonScheme, out personId);

  public static bool TryParseAttachmentId(string url, out int attachmentId) => TryParseId(url, AttachmentScheme, out attachmentId);

  public static bool TryParseMediaId(string url, out int mediaId) => TryParseId(url, MediaScheme, out mediaId);

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

  // TryParse rather than Parse because anything a user can type into a biography reaches this: an id
  // too long for an int is prose that happens to look like a link, not a link.
  private static bool TryParseId(string url, string scheme, out int id)
  {
    id = 0;
    return url.StartsWith(scheme, StringComparison.Ordinal) && int.TryParse(url.AsSpan(scheme.Length), out id);
  }

  // The alternation is the whole range check: 100-299, or 1-99 with no leading zero; anything else stays caption.
  [GeneratedRegex(@"(?:^|\s)([1-2][0-9]{2}|[1-9][0-9]?)%$")]
  private static partial Regex ImageWidthPattern();
}
