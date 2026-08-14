using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Dto;

public record class ImageDataWithMaxSize(
  int Id,
  byte[] Content,
  string? MimeType,
  DataCategory Category,
  int MaxSize) : Data(Id, Content, MimeType, Category)
{
  public ImageDataWithMaxSize(Data source, int maxSize)
    : this(source.Id, source.Content, source.MimeType, source.Category, maxSize)
  { }
}
