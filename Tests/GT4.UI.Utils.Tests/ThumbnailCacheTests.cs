using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
using Xunit;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Tests;

/// <summary>
/// Covers the cache's routing, key/scope and content-awareness logic. The actual downsize-and-write path
/// goes through MAUI's PlatformImage, which only runs on a real platform, so it is exercised by the running
/// app rather than here; these tests mock <see cref="IFileSystem"/> and assert the decisions around it.
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
  }

  private ThumbnailCache CreateCache() => new(_project.Object, _tokens.Object, _fs.Object, _storage.Object);

  private static Data Reference(int id) => new(id, [], "image/png", DataCategory.PersonMainPhoto);
  private static Data WithContent(int id) => new(id, [1, 2, 3, 4], "image/png", DataCategory.PersonMainPhoto);

  private void SetupBatchLoad() =>
    _data.Setup(d => d.GetDataByIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((int[] ids, CancellationToken _) => ids.ToDictionary(id => id, WithContent));

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
}
