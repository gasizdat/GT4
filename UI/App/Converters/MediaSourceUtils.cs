using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.UI.Utils;

namespace GT4.UI.Converters;

/// <summary>
/// Builds the id-to-data-URI map <see cref="Components.MarkdownView.MediaSources"/> uses to inline-render
/// a <c>media:&lt;id&gt;</c> reference. Lives in UI.App (not UI.Utils) because unwrapping a tagged photo's
/// residue envelope needs Core.Gedcom, same reason as <see cref="PhotoTagDataConverter"/>.
/// </summary>
public static class MediaSourceUtils
{
  // An OBJE with no FORM imports as a photo with no MIME type, and an empty data-URI media type means
  // text/plain (RFC 2397), which is never renderable. Every engine decodes an <img> by its bytes, so any
  // image type serves; this labels the URI only, leaving the stored null MimeType (and its GEDCOM
  // round-trip through GedcomMedia.ToForm) untouched.
  private const string FallbackImageMimeType = System.Net.Mime.MediaTypeNames.Image.Png;

  // Only a committed photo has a stable id a stored "media:id" reference can target; encoding one the
  // biography doesn't reference would just retain its bytes as a string for no reason (#211).
  public static IReadOnlyDictionary<int, string> BuildMediaSources(IEnumerable<Data> media, string? biography)
  {
    var referencedIds = MediaLinkUtils.ExtractReferencedIds(biography);
    return media
      .Where(item => item.Id != ElementId.NonCommittedId && referencedIds.Contains(item.Id) && IsInlineImage(item))
      .ToDictionary(item => item.Id, ToDataUri);
  }

  // A photo is an image by construction (its MIME may even be null); an attachment only when its own
  // MIME type says so -- an attached scan is just as renderable as a photo.
  public static bool IsInlineImage(Data media) =>
    media.Category.IsPhoto() || media.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

  // A tagged photo or an attachment's Content is a GedcomPhotoResidue envelope, not raw bytes -- strip it
  // first. Every other category stores raw bytes directly.
  public static byte[] PayloadBytes(Data data) =>
    data.Category.IsTaggedPhoto() || data.Category.IsAttachment()
      ? GedcomPhotoResidue.ExtractImageBytes(data.Content)
      : data.Content;

  private static string ToDataUri(Data media)
  {
    var mimeType = media.MimeType ?? FallbackImageMimeType;
    return $"data:{mimeType};base64,{Convert.ToBase64String(PayloadBytes(media))}";
  }
}
