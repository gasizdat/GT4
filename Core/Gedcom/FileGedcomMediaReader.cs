using GT4.Core.Gedcom.Abstraction;

namespace GT4.Core.Gedcom;

internal sealed class FileGedcomMediaReader : IGedcomMediaReader
{
  public byte[]? TryRead(string path)
  {
    try
    {
      return File.ReadAllBytes(path);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
      return null;
    }
  }
}
