using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.UI.Converters;

/// <summary>
/// Builds the id-to-data-URI map <see cref="Components.MarkdownView.MediaSources"/> uses to inline-render
/// a <c>media:&lt;id&gt;</c> reference. Lives in UI.App (not UI.Utils) because unwrapping a tagged photo's
/// residue envelope needs Core.Gedcom, same reason as <see cref="PhotoTagDataConverter"/>.
/// </summary>
public static class MediaSourceUtils
{
  // Only a committed photo has a stable id a stored "media:id" reference can target.
  public static IReadOnlyDictionary<int, string> BuildMediaSources(IEnumerable<Data> media) =>
    media
      .Where(item => item.Id != ElementId.NonCommittedId && IsInlineImage(item))
      .ToDictionary(item => item.Id, ToDataUri);

  // A photo is an image by construction (its MIME may even be null); an attachment only when its own
  // MIME type says so -- an attached scan is just as renderable as a photo.
  public static bool IsInlineImage(Data media) =>
    media.Category.IsPhoto() || media.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

  private static string ToDataUri(Data media)
  {
    var hasResidue = media.Category.IsTaggedPhoto() || media.Category.IsAttachment();
    var bytes = hasResidue ? GedcomPhotoResidue.ExtractImageBytes(media.Content) : media.Content;
    return $"data:{media.MimeType};base64,{Convert.ToBase64String(bytes)}";
  }
}
