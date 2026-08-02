using GT4.UI.Resources;
using System.IO.Compression;

namespace GT4.UI.Utils;

/// <summary>
/// What an import needs from a picked file: a way to reopen its GEDCOM text, and the folder its external
/// <c>OBJE FILE</c> media resolves against. A <c>.ged.zip</c> -- what this app's own export produces -- is
/// unpacked into a temporary folder first, so from here on a package and a loose .ged are the same thing.
/// </summary>
public sealed class GedcomImportSource : IDisposable
{
  private readonly string? _TempPath;

  private GedcomImportSource(Func<Task<Stream>> openStreamAsync, string? mediaBasePath, string name, string? tempPath)
  {
    OpenStreamAsync = openStreamAsync;
    MediaBasePath = mediaBasePath;
    Name = name;
    _TempPath = tempPath;
  }

  public Func<Task<Stream>> OpenStreamAsync { get; }

  /// <summary>Null when the platform gives no real path (a mobile content URI), leaving external media
  /// references unresolved and preserved as residue.</summary>
  public string? MediaBasePath { get; }

  /// <summary>The GEDCOM file's own name, without extension -- what a fresh project is named after.</summary>
  public string Name { get; }

  public static async Task<GedcomImportSource> OpenAsync(FileResult file, string tempRoot)
  {
    if (file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
      return await UnpackAsync(file.OpenReadAsync, tempRoot);

    var basePath = string.IsNullOrEmpty(file.FullPath) ? null : Path.GetDirectoryName(file.FullPath);
    return new GedcomImportSource(file.OpenReadAsync, basePath, Path.GetFileNameWithoutExtension(file.FileName), null);
  }

  // Seam for testing without FileResult, whose path-based constructor doesn't produce a working instance on
  // Windows -- the same reason GedcomImportEncoding takes an opener rather than the picked file.
  internal static async Task<GedcomImportSource> UnpackAsync(Func<Task<Stream>> openStreamAsync, string tempRoot)
  {
    // The staging file sits beside, not inside, the extraction directory: deleting extracted on any
    // failure below is then always the same one recursive delete, with nothing left over to special-case.
    var archivePath = Path.Combine(tempRoot, Path.GetRandomFileName());
    var extracted = Path.Combine(tempRoot, Path.GetRandomFileName());
    Directory.CreateDirectory(extracted);

    try
    {
      // Copied to disk first: a picked stream need not be seekable, and ZipArchive requires it.
      await using (var source = await openStreamAsync())
      await using (var target = new FileStream(archivePath, FileMode.Create))
      {
        await source.CopyToAsync(target);
      }
      ZipFile.ExtractToDirectory(archivePath, extracted);

      var gedcomPath = Directory.EnumerateFiles(extracted, "*.ged", SearchOption.AllDirectories).FirstOrDefault();
      if (gedcomPath is null)
        throw new InvalidDataException(UIStrings.AlertImportGedcomArchiveEmpty);

      return new GedcomImportSource(
        () => Task.FromResult<Stream>(File.OpenRead(gedcomPath)),
        Path.GetDirectoryName(gedcomPath),
        Path.GetFileNameWithoutExtension(gedcomPath),
        extracted);
    }
    catch
    {
      Directory.Delete(extracted, recursive: true);
      throw;
    }
    finally
    {
      File.Delete(archivePath);
    }
  }

  public void Dispose()
  {
    if (_TempPath is not null)
    {
      Directory.Delete(_TempPath, recursive: true);
    }
  }
}
