using GT4.Core.Project.Dto;
using Microsoft.Extensions.Http;

namespace GT4.UI.Utils.Converters;

public class ImageDataConverter(IHttpClientFactory httpClientFactory) : IDataConverter
{
  const string MimeTypeBmp = System.Net.Mime.MediaTypeNames.Image.Bmp;

  public async Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    var image = data as ImageSource;
    var content = image is null ? null : await ImageUtils.ToBytesAsync(image, httpClientFactory, token);

    return content is null ? null : new Data(
      Id: ElementId.NonCommittedId,
      Content: content,
      MimeType: MimeTypeBmp,
      Category: default);
  }

  public virtual Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    return Task.FromResult<object?>(data is null ? null : ImageUtils.ImageFromBytes(data.Content));
  }
}
