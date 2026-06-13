using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

using IFileSystem = GT4.Core.Utils.IFileSystem;

/// <summary>
/// Covers <see cref="ProjectList"/> end-to-end against real on-disk SQLite documents. A temp-directory
/// backed <see cref="IFileSystem"/> and <see cref="IStorage"/> stand in for the platform services so
/// the create/open/import/remove/list flows can run without touching the user's real folders.
/// </summary>
public sealed class ProjectListTests : IDisposable
{
  private readonly string _root = Path.Combine(Path.GetTempPath(), $"gt4_list_{Guid.NewGuid():N}");
  private readonly DiskFileSystem _fs;
  private readonly TempStorage _storage = new();
  private readonly ProjectList _list;
  private CancellationToken Token => TestContext.Current.CancellationToken;

  public ProjectListTests()
  {
    Directory.CreateDirectory(_root);
    _fs = new DiskFileSystem(_root);
    _list = new ProjectList(_storage, _fs);
  }

  public void Dispose()
  {
    try { Directory.Delete(_root, recursive: true); } catch { /* best-effort temp cleanup */ }
  }

  [Theory]
  [InlineData("My Tree", "My Tree")]
  [InlineData("A/B", "A2FB")] // '/' (0x2F) is not letter/digit/space, so it is hex-escaped.
  public void GetProjectDirectoryByName_SanitizesName(string name, string expectedLeaf)
  {
    var dir = _list.GetProjectDirectoryByName(name);

    dir.Path.Last().Should().Be(expectedLeaf);
    dir.Root.Should().Be(_storage.ProjectsRoot.Root);
  }

  /// <summary>
  /// Writes a fully valid project document directly at its library origin. CreateAsync's own
  /// cache-flush is timing dependent (it keys on the coarse TickCount64 revision), so the listing /
  /// open / remove / import assertions seed the origin deterministically instead.
  /// </summary>
  private async Task<FileDescription> SeedProjectAsync(string name, string description)
  {
    var dir = _list.GetProjectDirectoryByName(name);
    var origin = new FileDescription(dir, $"{name}-{Guid.NewGuid():N}.gt4", IProjectDocument.MimeType);
    var path = _fs.ToPath(origin);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await using var doc = await ProjectDocument.CreateNewAsync(path, name, Token);
    await doc.Metadata.SetProjectNameAsync(name, Token);
    await doc.Metadata.SetProjectDescriptionAsync(description, Token);
    return origin;
  }

  [Fact]
  public async Task Create_WritesProjectFilesUnderProjectsRoot()
  {
    await using (var host = await _list.CreateAsync("Brand New", "desc", Token)) { }

    // CreateAsync always materializes an origin under the projects root (independent of whether the
    // cache flush ran), so a *.gt4 file must exist there.
    _fs.GetFiles(_storage.ProjectsRoot, "*.gt4", recursive: true).Should().NotBeEmpty();
  }

  [Fact]
  public async Task GetItems_ReturnsSeededProject()
  {
    await SeedProjectAsync("Smiths", "The Smith family");

    var items = await _list.GetItemsAsync(Token);

    items.Should().ContainSingle();
    items[0].Name.Should().Be("Smiths");
    items[0].Description.Should().Be("The Smith family");
  }

  [Fact]
  public async Task Open_ReadsMetadataFromOrigin()
  {
    var origin = await SeedProjectAsync("Openable", "desc");

    await using var host = await _list.OpenAsync(origin, Token);
    host.Project.Should().NotBeNull();
    (await host.Project!.Metadata.GetProjectNameAsync(Token)).Should().Be("Openable");
  }

  [Fact]
  public async Task GetItems_CachesResult()
  {
    await SeedProjectAsync("Cached", "d");

    var first = await _list.GetItemsAsync(Token);
    var second = await _list.GetItemsAsync(Token);

    second.Should().BeSameAs(first);
  }

  [Fact]
  public async Task GetItems_InvalidProjectFile_ReturnsErrorEntry()
  {
    // A *.gt4 file that is not a valid SQLite project must surface as an error entry, not throw.
    var dir = _storage.ProjectsRoot with { Path = [.. _storage.ProjectsRoot.Path, "broken"] };
    var bogus = new FileDescription(dir, "broken-1.gt4", null);
    using (var stream = _fs.OpenWriteStream(bogus))
    {
      stream.Write([1, 2, 3, 4], 0, 4);
    }

    var items = await _list.GetItemsAsync(Token);

    items.Should().ContainSingle();
    items[0].Name.Should().StartWith("Error:");
  }

  [Fact]
  public async Task Remove_DeletesTheProjectFile()
  {
    await SeedProjectAsync("ToRemove", "d");
    (await _list.GetItemsAsync(Token)).Should().ContainSingle();

    await _list.RemoveAsync("ToRemove", Token);

    (await _list.GetItemsAsync(Token)).Should().BeEmpty();
  }

  [Fact]
  public async Task Remove_UnknownName_IsNoOp()
  {
    await SeedProjectAsync("Keep", "d");

    await _list.RemoveAsync("does-not-exist", Token);

    (await _list.GetItemsAsync(Token)).Should().ContainSingle();
  }

  [Fact]
  public async Task Import_CopiesProjectIntoTheLibrary()
  {
    // Build a standalone project file, then import its bytes.
    var sourcePath = Path.Combine(_root, "import-source.gt4");
    await using (var doc = await ProjectDocument.CreateNewAsync(sourcePath, "Imported", Token))
    {
      await doc.Metadata.SetProjectNameAsync("Imported", Token);
    }

    ProjectInfo imported;
    await using (var content = File.OpenRead(sourcePath))
    {
      imported = await _list.ImportAsync(content, Token);
    }

    imported.Name.Should().Be("Imported");
    imported.Origin.Should().NotBeNull();
    _fs.FileExists(imported.Origin).Should().BeTrue();

    var items = await _list.GetItemsAsync(Token);
    items.Select(i => i.Name).Should().Contain("Imported");
  }
}

/// <summary><see cref="IStorage"/> with stable folders the <see cref="DiskFileSystem"/> roots under a temp dir.</summary>
internal sealed class TempStorage : IStorage
{
  public DirectoryDescription ProjectsCache => new(Environment.SpecialFolder.ApplicationData, ["GT4", ".cache"]);
  public DirectoryDescription ProjectsRoot => new(Environment.SpecialFolder.MyDocuments, ["GT4"]);
  public DirectoryDescription AppConfig => new(Environment.SpecialFolder.ApplicationData, ["GT4", ".config"]);
}

/// <summary>
/// A real-disk <see cref="IFileSystem"/> that maps every <see cref="DirectoryDescription"/> beneath a
/// single temp root, so tests get genuine file/SQLite behaviour without writing to the user's folders.
/// </summary>
internal sealed class DiskFileSystem(string root) : IFileSystem
{
  public string ToPath(DirectoryDescription directory) =>
    Path.Combine(new[] { root, directory.Root.ToString() }.Concat(directory.Path).ToArray());

  public string ToPath(FileDescription file) => Path.Combine(ToPath(file.Directory), file.FileName);

  public bool FileExists(FileDescription file) => File.Exists(ToPath(file));

  public DateTime GetLastWriteTime(FileDescription file) => File.GetLastWriteTime(ToPath(file));

  public Stream OpenWriteStream(FileDescription file)
  {
    var path = ToPath(file);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    return File.Create(path);
  }

  public Stream OpenReadStream(FileDescription file) => File.OpenRead(ToPath(file));

  public void Copy(FileDescription from, FileDescription to)
  {
    using var source = OpenReadStream(from);
    Copy(source, to);
  }

  public void Copy(Stream from, FileDescription to)
  {
    using var target = OpenWriteStream(to);
    from.CopyTo(target);
  }

  public void RemoveFile(FileDescription file)
  {
    var path = ToPath(file);
    if (File.Exists(path)) File.Delete(path);
  }

  public void RemoveDirectory(DirectoryDescription directory)
  {
    var path = ToPath(directory);
    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
  }

  public FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive)
  {
    var basePath = ToPath(directory);
    if (!Directory.Exists(basePath)) return [];

    var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    return Directory
      .GetFiles(basePath, searchPattern, option)
      .Select(fp =>
      {
        var relative = Path.GetRelativePath(basePath, fp);
        var relativeDir = Path.GetDirectoryName(relative);
        var subdirs = string.IsNullOrEmpty(relativeDir)
          ? Array.Empty<string>()
          : relativeDir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return new FileDescription(directory with { Path = [.. directory.Path, .. subdirs] }, Path.GetFileName(relative), null);
      })
      .ToArray();
  }
}
