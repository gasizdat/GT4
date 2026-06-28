using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
using Xunit;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Tests;

/// <summary>
/// Covers the cache's routing, key/scope, content-awareness and write/publish logic. The real downsizing
/// goes through MAUI's PlatformImage, which only runs on a platform, so it is replaced here by a faked
/// <see cref="IImageDownsizer"/>; the one piece left to the running app is <see cref="ThumbnailCache.Resolve"/>
/// constructing a <c>FileImageSource</c>, which also needs a platform.
/// </summary>
public sealed class ThumbnailCacheTests
{
  private static readonly DirectoryDescription Cache =
    new(Environment.SpecialFolder.ApplicationData, ["GT4", ".cache"]);

  private readonly Mock<IFileSystem> _fs = new();
  private readonly Mock<IStorage> _storage = new();
  private readonly Mock<ICurrentProjectProvider> _project = new();
  private readonly Mock<ICancellationTokenProvider> _tokens = new();
  private readonly Mock<IProjectDocument> _document = new();
  private readonly Mock<ITableData> _data = new();
  private readonly Mock<IImageDownsizer> _downsizer = new();

  private FileDescription _origin =
    new(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, ["projects"]), "tree.gt4", null);

  public ThumbnailCacheTests()
  {
    _storage.SetupGet(s => s.ProjectsCache).Returns(Cache);
    _project.SetupGet(p => p.HasCurrentProject).Returns(true);
    _project.SetupGet(p => p.Info).Returns(() => new ProjectInfo("name", "desc", "rev", _origin));
    _project.SetupGet(p => p.Project).Returns(_document.Object);
    _document.SetupGet(d => d.Data).Returns(_data.Object);

    _fs.Setup(f => f.FileExists(It.IsAny<FileDescription>())).Returns(false);
    _fs.Setup(f => f.ToPath(It.IsAny<FileDescription>()))
       .Returns((FileDescription file) => string.Join("/", [.. file.Directory.Path, file.FileName]));
    _fs.Setup(f => f.OpenWriteStream(It.IsAny<FileDescription>())).Returns(() => new MemoryStream());

    // Default to an undecodable image, so tests that only assert routing/scoping never reach an actual
    // write; the write-path tests override this with a deterministic downsize.
    _downsizer.Setup(d => d.Downsize(It.IsAny<byte[]>(), It.IsAny<float>())).Throws<NotSupportedException>();
  }

  private ThumbnailCache CreateCache(IFileSystem? fileSystem = null) =>
    new(_project.Object, _tokens.Object, fileSystem ?? _fs.Object, _storage.Object, _downsizer.Object);

  private static Data Reference(int id) => new(id, [], "image/png", DataCategory.PersonMainPhoto);
  private static Data WithContent(int id) => new(id, [1, 2, 3, 4], "image/png", DataCategory.PersonMainPhoto);

  private void SetupBatchLoad() =>
    _data.Setup(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((int[] ids, CancellationToken _) => ids.ToDictionary(id => id, WithContent));

  private void SetupDownsize(params byte[] result) =>
    _downsizer.Setup(d => d.Downsize(It.IsAny<byte[]>(), It.IsAny<float>())).Returns(result);

  [Fact]
  public void Resolve_NullPhoto_ReturnsNull()
  {
    CreateCache().Resolve(null).Should().BeNull();
  }

  [Fact]
  public void Resolve_NoCurrentProject_ReturnsNull()
  {
    _project.SetupGet(p => p.HasCurrentProject).Returns(false);

    CreateCache().Resolve(Reference(1)).Should().BeNull();
    _data.Verify(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public void Resolve_MissingAndContentLess_SelfHealsByLoadingItsBlob()
  {
    _tokens.Setup(t => t.CreateShortOperationCancellationToken())
           .Returns(new CancellationTokenHost(TimeSpan.FromSeconds(30)));
    SetupBatchLoad();

    CreateCache().Resolve(Reference(5));

    _data.Verify(d => d.GetDataByIdsAsync(It.Is<int[]>(ids => ids.SequenceEqual(new[] { 5 })),
                                          It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Prewarm_BatchLoadsOnlyContentLessReferences()
  {
    int[]? requested = null;
    _data.Setup(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((int[] ids, CancellationToken _) =>
         {
           requested = ids;
           return ids.ToDictionary(id => id, WithContent);
         });

    await CreateCache().PrewarmAsync([Reference(1), WithContent(2), Reference(3)], CancellationToken.None);

    requested.Should().BeEquivalentTo([1, 3]);
  }

  [Fact]
  public async Task Prewarm_AllContentPresent_DoesNotTouchDatabase()
  {
    await CreateCache().PrewarmAsync([WithContent(1), WithContent(2)], CancellationToken.None);

    _data.Verify(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Prewarm_AlreadyCached_DoesNotTouchDatabase()
  {
    _fs.Setup(f => f.FileExists(It.IsAny<FileDescription>())).Returns(true);

    await CreateCache().PrewarmAsync([Reference(1), Reference(2)], CancellationToken.None);

    _data.Verify(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Prewarm_DeduplicatesIds()
  {
    int[]? requested = null;
    _data.Setup(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((int[] ids, CancellationToken _) =>
         {
           requested = ids;
           return ids.ToDictionary(id => id, WithContent);
         });

    await CreateCache().PrewarmAsync([Reference(1), Reference(1), Reference(2)], CancellationToken.None);

    requested.Should().BeEquivalentTo([1, 2]);
  }

  [Fact]
  public async Task Prewarm_NoCurrentProject_DoesNothing()
  {
    _project.SetupGet(p => p.HasCurrentProject).Returns(false);

    await CreateCache().PrewarmAsync([Reference(1)], CancellationToken.None);

    _data.Verify(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Prewarm_KeysByIdUnderTheProjectScopedCacheDirectory()
  {
    var seen = new List<FileDescription>();
    _fs.Setup(f => f.FileExists(It.IsAny<FileDescription>()))
       .Returns((FileDescription file) => { seen.Add(file); return false; });
    SetupBatchLoad();

    await CreateCache().PrewarmAsync([Reference(42)], CancellationToken.None);

    seen.Should().OnlyContain(file => file.FileName == "42.png");
    var directory = seen.First().Directory.Path;
    directory.Take(3).Should().Equal("GT4", ".cache", "thumbs");
    directory.Should().HaveCount(4, "the project scope is the only segment under thumbs");
  }

  [Fact]
  public async Task Prewarm_DifferentProjects_UseDifferentScopes()
  {
    var seen = new List<FileDescription>();
    _fs.Setup(f => f.FileExists(It.IsAny<FileDescription>()))
       .Returns((FileDescription file) => { seen.Add(file); return false; });
    SetupBatchLoad();
    var cache = CreateCache();

    await cache.PrewarmAsync([Reference(1)], CancellationToken.None);
    var firstScope = seen.Select(file => file.Directory.Path[3]).Distinct().Single();

    seen.Clear();
    _origin = _origin with { FileName = "other.gt4" };
    await cache.PrewarmAsync([Reference(1)], CancellationToken.None);
    var secondScope = seen.Select(file => file.Directory.Path[3]).Distinct().Single();

    secondScope.Should().NotBe(firstScope);
  }

  [Fact]
  public async Task Prewarm_ContentBearing_WritesDownsizedThumbnailKeyedById()
  {
    var fileSystem = new InMemoryFileSystem();
    SetupDownsize(9, 9);

    await CreateCache(fileSystem).PrewarmAsync([WithContent(2)], CancellationToken.None);

    var file = fileSystem.Files.Should().ContainSingle().Subject;
    file.Key.Should().EndWith("/2.png");
    file.Value.Should().Equal(9, 9);
    _downsizer.Verify(d => d.Downsize(It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3, 4 })), 200f), Times.Once);
  }

  [Fact]
  public async Task Prewarm_ContentLess_WritesDownsizedThumbnailAfterLoadingBlob()
  {
    var fileSystem = new InMemoryFileSystem();
    SetupBatchLoad();
    SetupDownsize(7);

    await CreateCache(fileSystem).PrewarmAsync([Reference(5)], CancellationToken.None);

    var file = fileSystem.Files.Should().ContainSingle().Subject;
    file.Key.Should().EndWith("/5.png");
    file.Value.Should().Equal(7);
  }

  [Fact]
  public async Task Prewarm_UndecodableImage_WritesNothing()
  {
    var fileSystem = new InMemoryFileSystem();
    // _downsizer throws by default.

    await CreateCache(fileSystem).PrewarmAsync([WithContent(2)], CancellationToken.None);

    fileSystem.Files.Should().BeEmpty();
  }

  [Fact]
  public async Task Prewarm_PublishesThroughTempAndLeavesNoTempFile()
  {
    var fileSystem = new InMemoryFileSystem();
    SetupDownsize(1);

    await CreateCache(fileSystem).PrewarmAsync([WithContent(2)], CancellationToken.None);

    fileSystem.Files.Keys.Should().ContainSingle().Which.Should().EndWith("/2.png");
    fileSystem.Files.Keys.Should().NotContain(key => key.Contains(".tmp"));
  }

  [Fact]
  public async Task Prewarm_WhenPublishMoveFails_RemovesTheTempFile()
  {
    SetupDownsize(0);
    _fs.Setup(f => f.Move(It.IsAny<FileDescription>(), It.IsAny<FileDescription>())).Throws<IOException>();
    var removed = new List<FileDescription>();
    _fs.Setup(f => f.RemoveFile(It.IsAny<FileDescription>())).Callback((FileDescription file) => removed.Add(file));

    await CreateCache().PrewarmAsync([WithContent(2)], CancellationToken.None);

    removed.Should().ContainSingle().Which.FileName.Should().EndWith(".tmp");
  }

  // A real-ish in-memory file system: keeps written bytes per path and moves them atomically, so the
  // cache's temp-write-then-publish can be exercised without a platform.
  private sealed class InMemoryFileSystem : IFileSystem
  {
    public Dictionary<string, byte[]> Files { get; } = new();

    public string ToPath(DirectoryDescription directory) => string.Join("/", directory.Path);
    public string ToPath(FileDescription file) => $"{ToPath(file.Directory)}/{file.FileName}";
    public bool FileExists(FileDescription file) => Files.ContainsKey(ToPath(file));

    public Stream OpenWriteStream(FileDescription file)
    {
      var path = ToPath(file);
      return new CapturingStream(bytes => Files[path] = bytes);
    }

    public void Move(FileDescription from, FileDescription to)
    {
      var source = ToPath(from);
      var destination = ToPath(to);
      if (Files.ContainsKey(destination))
        throw new IOException($"{destination} already exists");
      Files[destination] = Files[source];
      Files.Remove(source);
    }

    public void RemoveFile(FileDescription file) => Files.Remove(ToPath(file));

    public Stream OpenReadStream(FileDescription file) => throw new NotSupportedException();
    public void Copy(FileDescription from, FileDescription to) => throw new NotSupportedException();
    public void Copy(Stream from, FileDescription to) => throw new NotSupportedException();
    public FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive) => throw new NotSupportedException();
    public void RemoveDirectory(DirectoryDescription directory) => throw new NotSupportedException();
    public DateTime GetLastWriteTime(FileDescription file) => throw new NotSupportedException();
  }

  private sealed class CapturingStream(Action<byte[]> onClose) : MemoryStream
  {
    protected override void Dispose(bool disposing)
    {
      if (disposing)
        onClose(ToArray());
      base.Dispose(disposing);
    }
  }
}
