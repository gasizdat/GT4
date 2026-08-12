using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;

namespace GT4.UI.Utils.Converters;

public sealed class ImageDataConverter : IDataConverter
{
  const string MimeTypeBmp = System.Net.Mime.MediaTypeNames.Image.Bmp;

  private readonly IHttpClientFactory _HttpClientFactory;
  private readonly IImageCache _ImageCache;

  public ImageDataConverter(IHttpClientFactory httpClientFactory, IImageCache imageCache)
  {
    _HttpClientFactory = httpClientFactory;
    _ImageCache = imageCache;
  }

  public async Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    var image = data is PhotoInfo photo ? photo.Source : null;
    var content = image is null ? null : await ImageUtils.ToBytesAsync(image, _HttpClientFactory, token);

    return content is null ? null : new Data(
      Id: ElementId.NonCommittedId,
      Content: content,
      MimeType: MimeTypeBmp,
      Category: default);
  }

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null)
    {
      return null;
    }

    ImageSource imageSource;
    if (data.Id == ElementId.NonCommittedId)
    {
      imageSource = ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data.Content), token));
    }
    else
    {
      var key = $"{nameof(ImageDataConverter)}_{data.Id}";
      imageSource = _ImageCache.GetImage(key, () => data.Content);
    }

    return new PhotoInfo(imageSource, null);
  }
}
