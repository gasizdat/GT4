using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.UI;

namespace GT4;
public class ImageDataConverter : IDataConverter
{
  const string MimeTypeBmp = System.Net.Mime.MediaTypeNames.Image.Bmp;

  public async Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    var image = data as ImageSource;
    var content = image is null ? null : await ImageUtils.ToBytesAsync(image, token);

    return content is null ? null : new Data(
      Id: TableBase.NonCommitedId,
      Content: content,
      MimeType: MimeTypeBmp,
      Category: default);
  }

  public Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    return Task.FromResult<object?>(data is null ? null : ImageUtils.ImageFromBytes(data.Content));
  }
}
