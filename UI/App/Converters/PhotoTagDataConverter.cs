using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Http;

namespace GT4.UI.Converters;

/// <summary>
/// Decodes a tagged photo (<see cref="DataCategory.PersonMainPhotoTagged"/>/
/// <see cref="DataCategory.PersonPhotoTagged"/>) into its image and caption, same output type as
/// <see cref="ImageDataConverter"/> so every existing photo display call site needs no further change.
/// Composes an <see cref="ImageDataConverter"/> rather than subclassing it: a modification can never
/// legitimately carry the old photo's tags (there is no editable-caption UI in this pass), so
/// FromObjectAsync must encode as plain image bytes exactly like ImageDataConverter would -- inheriting
/// it would have ToObjectAsync return a captioned PhotoInfo that FromObjectAsync couldn't round-trip.
/// The resulting Data's Category gets downgraded from tagged to plain by PersonDataItem.ToDataAsync
/// (DataCategoryExtensions.AsPlainPhoto). Lives in UI.App (not UI.Utils) because unwrapping the residue
/// envelope needs Core.Gedcom.
/// </summary>
public sealed class PhotoTagDataConverter : IDataConverter
{
  private readonly ImageDataConverter _ImageConverter;

  public PhotoTagDataConverter(IHttpClientFactory httpClientFactory)
  {
    _ImageConverter = new ImageDataConverter(httpClientFactory);
  }

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token) =>
    _ImageConverter.FromObjectAsync(data, token);

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null)
      return null;

    var imageBytes = GedcomPhotoResidue.PayloadBytes(data);
    var caption = await GedcomPhotoResidue.ExtractTitleAsync(data, token);
    return new PhotoInfo(ImageUtils.ImageFromBytes(imageBytes), caption);
  }
}
