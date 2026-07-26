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
  public static IReadOnlyDictionary<int, string> BuildMediaSources(IEnumerable<Data> photos) =>
    photos
      .Where(photo => photo.Id != ElementId.NonCommittedId)
      .ToDictionary(photo => photo.Id, ToDataUri);

  private static string ToDataUri(Data photo)
  {
    var bytes = photo.Category.IsTaggedPhoto() ? GedcomPhotoResidue.ExtractImageBytes(photo.Content) : photo.Content;
    return $"data:{photo.MimeType};base64,{Convert.ToBase64String(bytes)}";
  }
}
