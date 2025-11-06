namespace GT4.Core.Utils;

public record class FileDescription(
  DirectoryDescription Directory,
  string FileName,
  string? MimeType
)
{
  public override int GetHashCode()
  {
    return HashCode.Combine(
      Directory.GetHashCode(),
      FileName.GetHashCode(),
      MimeType?.GetHashCode());
  }

  public virtual bool Equals(FileDescription? other)
  {
    return Directory == other?.Directory && FileName == other?.FileName && MimeType == other?.MimeType;
  }
}
