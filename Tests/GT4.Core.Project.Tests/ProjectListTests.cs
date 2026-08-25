using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
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
  /// Writes a fully valid project document directly at its library origin, used to seed the
  /// listing / open / remove tests independently of the CreateAsync path under test elsewhere.
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

  private DirectoryDescription CacheDirectory(string projectFolder) =>
    _storage.ProjectsCache with { Path = [.. _storage.ProjectsCache.Path, projectFolder] };

  /// <summary>Copies a project file into the revision cache, the way a session that was killed before
  /// disposing its <see cref="ProjectHost"/> leaves it behind.</summary>
  private FileDescription SeedRevision(string projectFolder, string fileName, FileDescription source, DateTime lastWrite)
  {
    var revision = new FileDescription(CacheDirectory(projectFolder), fileName, IProjectDocument.MimeType);
    _fs.Copy(source, revision);
    Touch(revision, lastWrite);
    return revision;
  }

  private void Touch(FileDescription file, DateTime lastWrite) =>
    File.SetLastWriteTime(_fs.ToPath(file), lastWrite);

  /// <summary>Commits an empty transaction, which stamps the next revision counter into the file.</summary>
  private async Task BumpRevisionAsync(FileDescription revision)
  {
    await using var doc = await ProjectDocument.OpenAsync(_fs.ToPath(revision), Token);
    using var transaction = await doc.BeginTransactionAsync(Token);
    await transaction.CommitAsync(Token);
  }

  [Fact]
  public async Task Create_PersistsProjectToOrigin_AndListsIt()
  {
    // CreateAsync must return a live, usable host and flush the freshly written cache back to the
    // origin on dispose, so the project is fully persisted.
    await using (var host = await _list.CreateAsync("Brand New", "The description", Token))
    {
      host.Project.Should().NotBeNull();
      (await host.Project!.Metadata.GetProjectNameAsync(Token)).Should().Be("Brand New");
    }

    _fs.GetFiles(_storage.ProjectsRoot, "*.gt4", recursive: true).Should().NotBeEmpty();

    var items = await _list.GetItemsAsync(Token);
    items.Should().ContainSingle();
    items[0].Name.Should().Be("Brand New");
    items[0].Description.Should().Be("The description");
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
  public async Task GetItems_FolderEnumerationFailure_Propagates()
  {
    // A failure enumerating the projects folder itself (as opposed to a single broken project file,
    // which GetProjectInfoAsync already turns into an error entry) must not be swallowed into an
    // empty list - the caller needs to know listing failed.
    var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
    fileSystem
      .Setup(fs => fs.GetFiles(_storage.ProjectsRoot, $"*.{ProjectList.ProjectExtension}", true))
      .Throws(new IOException("disk unavailable"));
    var list = new ProjectList(_storage, fileSystem.Object);

    var act = () => list.GetItemsAsync(Token);

    await act.Should().ThrowAsync<IOException>();
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

  [Fact]
  public async Task Remove_DeletesOriginFile_AndInvalidatesCachedListing()
  {
    var origin = await SeedProjectAsync("Doomed", "to be removed");
    // Populate the listing cache first, so the test also proves Remove invalidates it rather than
    // leaving a stale entry behind.
    (await _list.GetItemsAsync(Token)).Should().ContainSingle();

    await _list.RemoveAsync(origin, Token);

    _fs.FileExists(origin).Should().BeFalse();
    (await _list.GetItemsAsync(Token)).Should().BeEmpty();
  }

  [Fact]
  public async Task Remove_ByOrigin_DeletesOnlyThatProject_NotASameNamedSibling()
  {
    // The reason RemoveAsync keys on the origin file rather than the display name: two projects can share
    // a name (e.g. re-importing the same GEDCOM), and removing one must not delete the other.
    var first = await SeedProjectAsync("Twins", "first");
    var second = await SeedProjectAsync("Twins", "second");

    await _list.RemoveAsync(first, Token);

    _fs.FileExists(first).Should().BeFalse();
    _fs.FileExists(second).Should().BeTrue();
    (await _list.GetItemsAsync(Token)).Should().ContainSingle().Which.Origin.Should().Be(second);
  }

  [Fact]
  public async Task SanitizeRevisions_IdenticalLeftovers_KeepOnlyTheNewest()
  {
    var origin = await SeedProjectAsync("Crashy", "d");
    var older = SeedRevision("Crashy", "version-1.gt4", origin, new DateTime(2026, 1, 1));
    var middle = SeedRevision("Crashy", "version-2.gt4", origin, new DateTime(2026, 2, 1));
    var newest = SeedRevision("Crashy", "version-3.gt4", origin, new DateTime(2026, 3, 1));

    await _list.SanitizeRevisionsAsync(Token);

    _fs.FileExists(newest).Should().BeTrue();
    _fs.FileExists(middle).Should().BeFalse();
    _fs.FileExists(older).Should().BeFalse();
  }

  [Fact]
  public async Task SanitizeRevisions_LeftoverHoldingACommit_Survives()
  {
    // A crash after a commit but before dispose leaves a cache file whose counter ran ahead of the
    // origin - the only copy of that work. Every file reports the same size here, so the revision
    // counter is provably the only thing keeping it alive.
    var origin = await SeedProjectAsync("Unflushed", "d");
    var committed = SeedRevision("Unflushed", "version-1.gt4", origin, new DateTime(2026, 1, 1));
    await BumpRevisionAsync(committed);
    Touch(committed, new DateTime(2026, 1, 1));
    var staleOlder = SeedRevision("Unflushed", "version-2.gt4", origin, new DateTime(2026, 2, 1));
    var staleNewer = SeedRevision("Unflushed", "version-3.gt4", origin, new DateTime(2026, 3, 1));

    var list = new ProjectList(_storage, new DiskFileSystem(_root, uniformFileSize: true));
    await list.SanitizeRevisionsAsync(Token);

    _fs.FileExists(committed).Should().BeTrue();
    _fs.FileExists(staleNewer).Should().BeTrue();
    _fs.FileExists(staleOlder).Should().BeFalse();
  }

  [Fact]
  public async Task SanitizeRevisions_ComparesWithinOneProjectOnly()
  {
    // Both projects' leftovers are byte-identical, so grouping that ignored the project would collapse
    // all four into one and leave a single survivor overall.
    var origin = await SeedProjectAsync("Shared", "d");
    var firstOlder = SeedRevision("ProjectA", "version-1.gt4", origin, new DateTime(2026, 1, 1));
    var firstNewer = SeedRevision("ProjectA", "version-2.gt4", origin, new DateTime(2026, 2, 1));
    var secondOlder = SeedRevision("ProjectB", "version-1.gt4", origin, new DateTime(2026, 1, 1));
    var secondNewer = SeedRevision("ProjectB", "version-2.gt4", origin, new DateTime(2026, 2, 1));

    await _list.SanitizeRevisionsAsync(Token);

    _fs.FileExists(firstNewer).Should().BeTrue();
    _fs.FileExists(secondNewer).Should().BeTrue();
    _fs.FileExists(firstOlder).Should().BeFalse();
    _fs.FileExists(secondOlder).Should().BeFalse();
  }

  [Fact]
  public async Task SanitizeRevisions_LeavesTheSurvivorsTimestampUntouched()
  {
    // RemoveRevisionAsync and RestoreRevisionAsync both reject a ProjectRevision whose recorded mtime no
    // longer matches the file, so reading a revision's counter must not perturb it.
    var origin = await SeedProjectAsync("Untouched", "d");
    var lastWrite = new DateTime(2026, 3, 1);
    SeedRevision("Untouched", "version-1.gt4", origin, new DateTime(2026, 1, 1));
    var newest = SeedRevision("Untouched", "version-2.gt4", origin, lastWrite);

    await _list.SanitizeRevisionsAsync(Token);

    _fs.GetLastWriteTime(newest).Should().Be(lastWrite);
  }

  [Fact]
  public async Task SanitizeRevisions_UnreadableRevisions_AreLeftAlone()
  {
    // Same size, so both are opened - and neither yields a counter to be compared by.
    var cache = CacheDirectory("Broken");
    var first = new FileDescription(cache, "version-1.gt4", IProjectDocument.MimeType);
    var second = new FileDescription(cache, "version-2.gt4", IProjectDocument.MimeType);
    foreach (var junk in new[] { first, second })
    {
      using var stream = _fs.OpenWriteStream(junk);
      stream.Write([1, 2, 3, 4], 0, 4);
    }

    await _list.SanitizeRevisionsAsync(Token);

    _fs.FileExists(first).Should().BeTrue();
    _fs.FileExists(second).Should().BeTrue();
  }

  [Fact]
  public async Task SanitizeRevisions_RemovesTheSqliteSidecarsToo()
  {
    var origin = await SeedProjectAsync("Journaled", "d");
    var older = SeedRevision("Journaled", "version-1.gt4", origin, new DateTime(2026, 1, 1));
    SeedRevision("Journaled", "version-2.gt4", origin, new DateTime(2026, 2, 1));
    // Truncated to zero on the last commit and never unlinked, which is what a killed session leaves.
    var journal = older with { FileName = $"{older.FileName}-journal" };
    using (var stream = _fs.OpenWriteStream(journal)) stream.Close();

    await _list.SanitizeRevisionsAsync(Token);

    _fs.FileExists(older).Should().BeFalse();
    _fs.FileExists(journal).Should().BeFalse();
  }

  [Fact]
  public async Task Remove_NonExistentOrigin_IsANoOp()
  {
    var origin = await SeedProjectAsync("Survivor", "stays");
    var dir = _list.GetProjectDirectoryByName("Ghost");
    var missing = new FileDescription(dir, "ghost-never-written.gt4", IProjectDocument.MimeType);

    await _list.RemoveAsync(missing, Token);

    _fs.FileExists(origin).Should().BeTrue();
    (await _list.GetItemsAsync(Token)).Should().ContainSingle();
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
/// <param name="uniformFileSize">Reports every file as the same size, collapsing the revision sweep's
/// size pre-filter so a test can prove the revision counter alone decides what survives.</param>
internal sealed class DiskFileSystem(string root, bool uniformFileSize = false) : IFileSystem
{
  public string ToPath(DirectoryDescription directory) =>
    Path.Combine(new[] { root, directory.Root.ToString() }.Concat(directory.Path).ToArray());

  public string ToPath(FileDescription file) => Path.Combine(ToPath(file.Directory), file.FileName);

  public bool FileExists(FileDescription file) => File.Exists(ToPath(file));

  public DateTime GetLastWriteTime(FileDescription file) => File.GetLastWriteTime(ToPath(file));

  public long GetFileSize(FileDescription file) => uniformFileSize ? 0 : new FileInfo(ToPath(file)).Length;

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
