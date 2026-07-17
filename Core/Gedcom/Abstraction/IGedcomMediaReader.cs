namespace GT4.Core.Gedcom.Abstraction;

/// <summary>
/// Reads external media referenced by a GEDCOM <c>OBJE FILE</c>. The seam keeps raw file I/O out of the
/// importer, so platform-specific media access can be substituted.
/// </summary>
public interface IGedcomMediaReader
{
  /// <returns> The file content, or null when the file is missing or unreadable. </returns>
  byte[]? TryRead(string path);
}
