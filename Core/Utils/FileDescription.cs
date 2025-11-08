namespace GT4.Core.Utils;

/// <summary>
/// Describes a file
/// </summary>
/// <param name="Directory"> The file directory </param>
/// <param name="FileName"> The File name </param>
/// <param name="MimeType"> The file MIME type (optional) </param>
/// <remarks> MimeType is not considered in hash code generation or equality comparison </remarks>
public record class FileDescription(
  DirectoryDescription Directory,
  string FileName,
  string? MimeType
)
{
  public override int GetHashCode()
  {
    return HashCode.Combine(Directory.GetHashCode(), FileName.GetHashCode());
  }

  public virtual bool Equals(FileDescription? other)
  {
    return Directory == other?.Directory && FileName == other?.FileName;
  }
}
