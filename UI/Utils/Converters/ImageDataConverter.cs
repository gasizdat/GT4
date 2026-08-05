using GT4.Core.Project.Dto;
using Microsoft.Extensions.Http;

namespace GT4.UI.Utils.Converters;

public sealed class ImageDataConverter : IDataConverter
{
  const string MimeTypeBmp = System.Net.Mime.MediaTypeNames.Image.Bmp;

  private readonly IHttpClientFactory _HttpClientFactory;

  public ImageDataConverter(IHttpClientFactory httpClientFactory)
  {
    _HttpClientFactory = httpClientFactory;
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

  public Task<object?> ToObjectAsync(Data? data, CancellationToken token) => Task.FromResult<object?>(data is null ? null : new PhotoInfo(ImageUtils.ImageFromBytes(data.Content), null));
}
