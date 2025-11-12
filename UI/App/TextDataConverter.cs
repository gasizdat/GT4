using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.UI;

namespace GT4;
public class TextDataConverter : IDataConverter
{
  const string MimeTypePlainText = System.Net.Mime.MediaTypeNames.Text.Plain;

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    var ret = data switch
    {
      string text =>
        new Data(
          Id: TableBase.NonCommitedId,
          Content: System.Text.Encoding.UTF8.GetBytes(text),
          MimeType: MimeTypePlainText,
          Category: default),

      _ => null
    };

    return Task.FromResult(ret);
  }

  public Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    object? ret = data?.MimeType switch
    {
      MimeTypePlainText => System.Text.Encoding.UTF8.GetString(data.Content),
      _ => null
    };

    return Task.FromResult(ret);

  }
}
