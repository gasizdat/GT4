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

    ImageSource? imageSource;
    if (data is ImageDataWithMaxSize { Id: ElementId.NonCommittedId } uncommitted)
    {
      // Not cached: without an id there is no key that stays this image's own.
      var thumbnail = ImageUtils.DownsizedPng(data.Content, uncommitted.MaxSize);
      imageSource = thumbnail is null ? null : ImageSource.FromStream(() => new MemoryStream(thumbnail));
    }
    else if (data.Id == ElementId.NonCommittedId)
    {
      imageSource = ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data.Content), token));
    }
    else if (data is ImageDataWithMaxSize imageData)
    {
      var key = $"{nameof(ImageDataConverter)}_{data.Id}_{imageData.MaxSize}";
      imageSource = _ImageCache.GetImage(key, () => ImageUtils.DownsizedPng(data.Content, imageData.MaxSize));
    }
    else
    {
      var key = $"{nameof(ImageDataConverter)}_{data.Id}";
      imageSource = _ImageCache.GetImage(key, () => data.Content);
    }

    // Only a thumbnail decodes here; unlike a file that isn't an image, one that won't decode still gets a picture.
    var photo = new PhotoInfo(imageSource ?? ImageUtils.UnreadableImageStub, null);
    return Task.FromResult<object?>(photo);
  }
}
