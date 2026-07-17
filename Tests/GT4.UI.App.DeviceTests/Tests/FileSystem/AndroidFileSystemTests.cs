#if ANDROID
using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exercises the real <see cref="AndroidFileSystem"/> against MediaStore, in a throwaway
/// per-test-class folder under Documents. This is the only coverage of the external-storage
/// path (API 29+ needs no storage permission for the app's own MediaStore files).
/// </summary>
public sealed class AndroidFileSystemTests : IDisposable
{
  private readonly AndroidFileSystem _FileSystem = new();
  private readonly DirectoryDescription _Directory = new(
    Root: Environment.SpecialFolder.MyDocuments,
    Path: ["GT4-test", Guid.NewGuid().ToString("N")]);

  public void Dispose()
  {
    foreach (var file in _FileSystem.GetFiles(_Directory, "*", false))
    {
      _FileSystem.RemoveFile(file);
    }
  }

  [Fact]
  public void CreateExistsReadOverwriteRemove_RoundTrips()
  {
    var file = NewFile("roundtrip.bin");

    _FileSystem.FileExists(file).Should().BeFalse();

    using (var stream = _FileSystem.OpenWriteStream(file))
    {
      stream.Write("0123456789"u8);
    }
    _FileSystem.FileExists(file).Should().BeTrue();

    // "wt" must truncate: a shorter overwrite may not leave a tail of the first content behind
    using (var stream = _FileSystem.OpenWriteStream(file))
    {
      stream.Write("AB"u8);
    }

    using (var stream = _FileSystem.OpenReadStream(file))
    using (var reader = new StreamReader(stream))
    {
      reader.ReadToEnd().Should().Be("AB");
    }

    _FileSystem.RemoveFile(file);
    _FileSystem.FileExists(file).Should().BeFalse();
  }

  [Fact]
  public void OpenReadStream_MissingFile_Throws()
  {
    var file = NewFile("missing.bin");

    var act = () => _FileSystem.OpenReadStream(file);

    act.Should().Throw<IOException>();
  }

  [Fact]
  public void GetFiles_FiltersBySearchPattern()
  {
    var project = NewFile("sample.gt4");
    var note = NewFile("note.txt");
    Write(project, "p");
    Write(note, "n");

    var matches = _FileSystem.GetFiles(_Directory, "*.gt4", false);

    var match = matches.Should().ContainSingle().Subject;
    match.FileName.Should().Be("sample.gt4");
    match.Directory.Should().Be(_Directory);
  }

  private FileDescription NewFile(string fileName) =>
    new(_Directory, fileName, "application/octet-stream");

  private void Write(FileDescription file, string content)
  {
    using var stream = _FileSystem.OpenWriteStream(file);
    using var writer = new StreamWriter(stream);
    writer.Write(content);
  }
}
#endif
