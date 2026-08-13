using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Dto;

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

  public Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null || data.Content.Length == 0)
    {
      return Task.FromResult<object?>(null);
    }

    ImageSource imageSource;
    if (data.Id == ElementId.NonCommittedId)
    {
      imageSource = ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data.Content), token));
    }
    else if (data is ResizedImageData imageData)
    {
      var key = $"{nameof(ImageDataConverter)}_{data.Id}_{imageData.MaxSize}";
      imageSource = _ImageCache.GetImage(key, () => ImageUtils.DownsizedPng(data.Content, imageData.MaxSize));
    }
    else
    {
      var key = $"{nameof(ImageDataConverter)}_{data.Id}";
      imageSource = _ImageCache.GetImage(key, () => data.Content);
    }

    return Task.FromResult<object?>(new PhotoInfo(imageSource, null));
  }
}
