using System.IO.Compression;
using System.Text;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers unpacking a .ged.zip -- what the app's own export produces -- via the internal
/// Func&lt;Task&lt;Stream&gt;&gt; overload, since FileResult's path-based constructor does not produce a
/// working instance on Windows.
/// </summary>
public sealed class GedcomImportSourceTests
{
  private const string Gedcom = "0 HEAD\n1 CHAR UTF-8\n0 TRLR";

  private static Func<Task<Stream>> OpenPackage(params (string Path, byte[] Content)[] entries) => () =>
  {
    var buffer = new MemoryStream();
    using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
    {
      foreach (var (path, content) in entries)
      {
        using var stream = archive.CreateEntry(path).Open();
        stream.Write(content);
      }
    }
    buffer.Position = 0;
    return Task.FromResult<Stream>(buffer);
  };

  private static string TempRoot() => Path.Combine(Path.GetTempPath(), $"gt4_package_{Guid.NewGuid():N}");

  [Fact]
  public async Task UnpackAsync_ExposesTheGedcomAndTheFolderItsMediaResolvesAgainstAsync()
  {
    var image = new byte[] { 1, 2, 3 };
    var package = OpenPackage(("kennedy.ged", Encoding.UTF8.GetBytes(Gedcom)), ("media/7/photo.jpg", image));

    using var source = await GedcomImportSource.UnpackAsync(package, TempRoot());

    Assert.Equal("kennedy", source.Name);
    using var reader = new StreamReader(await source.OpenStreamAsync());
    Assert.Equal(Gedcom, (await reader.ReadToEndAsync()).ReplaceLineEndings("\n"));

    // The media base path is what an OBJE FILE of "media/7/photo.jpg" is resolved against.
    var mediaPath = Path.Combine(source.MediaBasePath!, "media", "7", "photo.jpg");
    Assert.Equal(image, await File.ReadAllBytesAsync(mediaPath, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UnpackAsync_ReopensTheGedcomOnEveryCallAsync()
  {
    // The encoding pass reads the header, then reopens to decode the whole file.
    var package = OpenPackage(("kennedy.ged", Encoding.UTF8.GetBytes(Gedcom)));

    using var source = await GedcomImportSource.UnpackAsync(package, TempRoot());

    await using (var first = await source.OpenStreamAsync())
    {
      Assert.NotEqual(0, first.ReadByte());
    }
    await using var second = await source.OpenStreamAsync();
    Assert.Equal(Gedcom.Length, second.Length);
  }

  [Fact]
  public async Task UnpackAsync_LeavesNothingBehindOnDisposeAsync()
  {
    var package = OpenPackage(("kennedy.ged", Encoding.UTF8.GetBytes(Gedcom)));
    var root = TempRoot();

    string extracted;
    using (var source = await GedcomImportSource.UnpackAsync(package, root))
    {
      extracted = source.MediaBasePath!;
      Assert.True(Directory.Exists(extracted));
    }

    Assert.False(Directory.Exists(extracted));
  }

  [Fact]
  public async Task UnpackAsync_AnArchiveWithoutAGedcomFailsAndCleansUpAsync()
  {
    var package = OpenPackage(("notes.txt", Encoding.UTF8.GetBytes("nothing here")));
    var root = TempRoot();

    await Assert.ThrowsAsync<InvalidDataException>(() => GedcomImportSource.UnpackAsync(package, root));

    Assert.Empty(Directory.EnumerateFileSystemEntries(root));
  }
}
