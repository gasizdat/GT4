using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.UI.Utils;

namespace GT4.UI.Converters;

/// <summary>
/// Builds the id-to-bytes map <see cref="Components.MarkdownView.MediaSources"/> uses to inline-render
/// a <c>media:&lt;id&gt;</c> reference. Lives in UI.App (not UI.Utils) because unwrapping a tagged photo's
/// residue envelope needs Core.Gedcom, same reason as <see cref="PhotoTagDataConverter"/>.
/// </summary>
public static class MediaSourceUtils
{
  // Only a committed photo has a stable id a stored "media:id" reference can target; retaining the bytes
  // of one the biography doesn't reference would serve no purpose (#211).
  public static IReadOnlyDictionary<int, byte[]> BuildMediaSources(IEnumerable<Data> media, string? biography)
  {
    var referencedIds = MediaLinkUtils.ExtractReferencedIds(biography);
    return media
      .Where(item => item.Id != ElementId.NonCommittedId && referencedIds.Contains(item.Id) && IsInlineImage(item))
      .ToDictionary(item => item.Id, PayloadBytes);
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
}
