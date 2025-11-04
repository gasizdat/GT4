namespace GT4.Core.Utils;

public record class FileDescription(
  DirectoryDescription Directory,
  string FileName,
  string? MimeType
);
