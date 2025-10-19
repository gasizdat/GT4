namespace GT4.Core.Project.Dto;

  public record class Data(
    int Id,
    byte[] Content,
    string MimeType
  );
