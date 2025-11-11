namespace GT4.Core.Project.Dto;

public record class DataWithCategory(
    int Id,
    byte[] Content,
    string MimeType,
    DataCategory Category)
    : Data(
      Id: Id,
      Content: Content,
      MimeType: MimeType)
{
  public DataWithCategory(Data data, DataCategory category)
    : this(Id: data.Id, Content: data.Content, MimeType: data.MimeType, Category: category)
  {
  }
}
