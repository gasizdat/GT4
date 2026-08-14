using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

/// <summary>
/// Decodes a tagged photo (<see cref="DataCategory.PersonMainPhotoTagged"/>/
/// <see cref="DataCategory.PersonPhotoTagged"/>) into its image and caption, same output type as
/// <see cref="ImageDataConverter"/> so every existing photo display call site needs no further change.
/// Composes an <see cref="ImageDataConverter"/> rather than subclassing it: this converter owns the
/// GedcomPhotoResidue envelope, the composed one owns bytes &lt;-&gt; ImageSource. A modification re-encodes
/// the caption so it survives an edit, and returns a tagged Category to signal that the Content is
/// enveloped. Only the caption is rebuilt: a PhotoInfo carries no other residual tag, so any other
/// imported OBJE child is lost on modification. Lives in UI.App (not UI.Utils) because unwrapping the
/// residue envelope needs Core.Gedcom.
/// </summary>
public sealed class PhotoTagDataConverter : IDataConverter
{
  private readonly ImageDataConverter _ImageConverter;

  public PhotoTagDataConverter(IHttpClientFactory httpClientFactory, IImageCache imageCache) =>
    _ImageConverter = new ImageDataConverter(httpClientFactory, imageCache);

  public async Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    var ret = await _ImageConverter.FromObjectAsync(data, token);
    if (ret is null || data is not PhotoInfo photo || string.IsNullOrEmpty(photo.Caption))
    {
      return ret;
    }

    var content = GedcomPhotoResidue.EncodePhotoTitle(ret.Content, photo.Caption);
    return ret with { Content = content, Category = DataCategory.PersonPhotoTagged };
  }

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null || data.Content.Length == 0)
    {
      return null;
    }

    var caption = await GedcomPhotoResidue.ExtractTitleAsync(data, token);
    var unwrapped = data with { Content = GedcomPhotoResidue.ExtractImageBytes(data.Content) };

    return await _ImageConverter.ToObjectAsync(unwrapped, token) is PhotoInfo photoInfo
      ? photoInfo with { Caption = caption }
      : null;
  }
}
