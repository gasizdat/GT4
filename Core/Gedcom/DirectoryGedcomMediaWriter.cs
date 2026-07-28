using GT4.Core.Gedcom.Abstraction;

namespace GT4.Core.Gedcom;

/// <summary>
/// Writes media as real files under <paramref name="basePath"/>, laid out exactly as the exported
/// <c>FILE</c> paths name them -- so the directory can be handed straight back to
/// <see cref="Abstraction.IGedcomImporter.ImportAsync"/> as its media base path.
/// </summary>
public sealed class DirectoryGedcomMediaWriter(string basePath) : IGedcomMediaWriter
{
  public async Task WriteAsync(string relativePath, byte[] content, CancellationToken token)
  {
    var path = Path.Combine(basePath, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllBytesAsync(path, content, token);
  }
}
