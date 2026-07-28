using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using System.IO.Compression;
using System.Text;

namespace GT4.Core.Gedcom;

/// <summary>
/// Packs an export into a single <c>.ged.zip</c>: the GEDCOM text plus the media files its <c>FILE</c>
/// references name. Packaging only -- the exporter itself knows nothing about zip, and a
/// <see cref="DirectoryGedcomMediaWriter"/> produces the same tree unpacked.
/// </summary>
public static class GedcomPackage
{
  public const string ArchiveExtension = ".ged.zip";

  /// <summary>
  /// A zip in Create mode allows one open entry at a time, so the GEDCOM text is buffered and written last,
  /// after the media entries the export produced. The text is small -- media no longer travels inside it.
  /// </summary>
  public static async Task WriteAsync(
    IGedcomExporter exporter,
    IProjectDocument document,
    Stream output,
    string gedcomEntryName,
    CancellationToken token)
  {
    using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
    var media = new ZipGedcomMediaWriter(archive);
    var text = new StringWriter();

    await exporter.ExportAsync(document, text, media, token);

    var entry = archive.CreateEntry(gedcomEntryName);
    await using var stream = entry.Open();
    await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
    await writer.WriteAsync(text.ToString());
  }

  private sealed class ZipGedcomMediaWriter(ZipArchive archive) : IGedcomMediaWriter
  {
    public async Task WriteAsync(string relativePath, byte[] content, CancellationToken token)
    {
      var entry = archive.CreateEntry(relativePath);
      await using var stream = entry.Open();
      await stream.WriteAsync(content, token);
    }
  }
}
