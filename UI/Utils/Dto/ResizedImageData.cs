using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Dto;

public record class ResizedImageData(
  int Id,
  byte[] Content,
  string? MimeType,
  DataCategory Category,
  int MaxSize) : Data(Id, Content, MimeType, Category)
{
  public ResizedImageData(Data source, int maxSize)
    : this(source.Id, source.Content, source.MimeType, source.Category, maxSize)
  { }
}
