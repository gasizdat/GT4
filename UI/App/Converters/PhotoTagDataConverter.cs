using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Converters;

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
  private readonly IImageCache _ImageCache;

  public PhotoTagDataConverter(IHttpClientFactory httpClientFactory, IImageCache imageCache)
  {
    _ImageConverter = new ImageDataConverter(httpClientFactory, imageCache);
    _ImageCache = imageCache;
  }

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token) =>
    _ImageConverter.FromObjectAsync(data, token);

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null)
    {
      return null;
    }

    ImageSource imageSource;
    var caption = await GedcomPhotoResidue.ExtractTitleAsync(data, token);

    if (data.Id == ElementId.NonCommittedId)
    {
      imageSource = ImageSource.FromStream(token =>
        Task.Run<Stream>(() => new MemoryStream(GedcomPhotoResidue.PayloadBytes(data)), token));
    }
    else
    {
      var key = $"{nameof(PhotoTagDataConverter)}_{data.Id}";
      imageSource = _ImageCache.GetImage(key, () => GedcomPhotoResidue.PayloadBytes(data));
    }

    return new PhotoInfo(imageSource, caption);
  }
}
