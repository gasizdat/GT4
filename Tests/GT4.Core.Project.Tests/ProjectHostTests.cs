using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Covers the cache/origin lifecycle owned by <see cref="ProjectHost"/>: the dispose-time decision
/// between flushing the cache back to the origin (revision changed) and removing it (unchanged),
/// the copy retry loop, and revision restore/remove/listing. Runs entirely on
/// <see cref="FileSystemMock"/> and a mocked <see cref="IProjectDocument"/> — no real SQLite.
/// </summary>
public sealed class ProjectHostTests
{
  private static readonly DirectoryDescription OriginDir = new(Environment.SpecialFolder.MyDocuments, ["projects"]);
  private static readonly DirectoryDescription CacheDir = new(Environment.SpecialFolder.LocalApplicationData, ["cache", "family"]);

  private readonly FileSystemMock _fs = new();
  private readonly FileDescription _origin = new(OriginDir, "family.gt4", null);
  private readonly FileDescription _cache = new(CacheDir, "version-current.gt4", null);
  private readonly Mock<IProjectDocument> _doc = new(MockBehavior.Strict);
  private long _revision = 100;

  public ProjectHostTests()
  {
    _fs.AddFile(_origin);
    _doc.SetupGet(d => d.ProjectRevision).Returns(() => _revision);
    _doc.Setup(d => d.Dispose());
    _doc.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);
  }

  /// <summary>Creates a host with the mocked document attached and the operation log reset.</summary>
  private ProjectHost CreateHost()
  {
    var host = new ProjectHost(_fs, _origin, _cache);
    host.Project = _doc.Object;
    _fs.ResetStats();
    return host;
  }

  private string CopyCacheToOrigin => $"copy {_fs.ToPath(_cache)} -> {_fs.ToPath(_origin)}";

  [Fact]
  public void Constructor_CopiesOriginToCache()
  {
    _ = new ProjectHost(_fs, _origin, _cache);

    _fs.FileExists(_cache).Should().BeTrue();
    _fs.Operations.Should().Equal($"copy {_fs.ToPath(_origin)} -> {_fs.ToPath(_cache)}");
  }

  [Fact]
  public async Task DisposeAsync_RevisionUnchanged_RemovesCache_WithoutTouchingOrigin()
  {
    var host = CreateHost();

    await host.DisposeAsync();

    _fs.FileExists(_cache).Should().BeFalse();
    _fs.Operations.Should().NotContain(op => op.StartsWith("copy"));
    _doc.Verify(d => d.DisposeAsync(), Times.Once);
    host.Project.Should().BeNull();
  }

  [Fact]
  public async Task DisposeAsync_RevisionChanged_CopiesCacheBackToOrigin()
  {
    var host = CreateHost();
    _revision++;

    await host.DisposeAsync();

    _fs.Operations.Should().Equal(CopyCacheToOrigin);
    // The cache is intentionally kept: it becomes a revision-history snapshot.
    _fs.FileExists(_cache).Should().BeTrue();
    _doc.Verify(d => d.DisposeAsync(), Times.Once);
  }

  [Fact]
  public async Task DisposeAsync_RevisionBumpedDuringDrain_StillCopiesCacheBackToOrigin()
  {
    // Regression test: a transaction committing while the document drains its in-flight work must
    // not be silently discarded. The host has to read the revision AFTER the dispose completes.
    _doc.Setup(d => d.DisposeAsync()).Returns(() =>
    {
      _revision++;
      return ValueTask.CompletedTask;
    });
    var host = CreateHost();

    await host.DisposeAsync();

    _fs.Operations.Should().Equal(CopyCacheToOrigin);
  }

  [Fact]
  public async Task DisposeAsync_RetriesFailedCopy_UntilItSucceeds()
  {
    var host = CreateHost();
    _revision++;
    _fs.CopyFailuresRemaining = 2;

    await host.DisposeAsync();

    _fs.CopyAttempts.Should().Be(3);
    _fs.Operations.Should().Equal(CopyCacheToOrigin);
  }

  [Fact]
  public async Task DisposeAsync_GivesUpQuietly_AfterFiveFailedAttempts()
  {
    var host = CreateHost();
    _revision++;
    _fs.CopyFailuresRemaining = int.MaxValue;

    // Documents current behavior: the failure is swallowed and the origin is left stale.
    await host.DisposeAsync();

    _fs.CopyAttempts.Should().Be(5);
    _fs.Operations.Should().BeEmpty();
  }

  [Fact]
  public async Task DisposeAsync_SecondCall_IsNoOp()
  {
    var host = CreateHost();

    await host.DisposeAsync();
    var operationCount = _fs.Operations.Count;
    await host.DisposeAsync();

    _fs.Operations.Should().HaveCount(operationCount);
    _doc.Verify(d => d.DisposeAsync(), Times.Once);
  }

  [Fact]
  public void Dispose_RevisionUnchanged_RemovesCache()
  {
    var host = CreateHost();

    host.Dispose();

    _fs.FileExists(_cache).Should().BeFalse();
    _fs.Operations.Should().NotContain(op => op.StartsWith("copy"));
    _doc.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void Dispose_RevisionChanged_CopiesCacheBackToOrigin()
  {
    var host = CreateHost();
    _revision++;

    host.Dispose();

    _fs.Operations.Should().Equal(CopyCacheToOrigin);
    _doc.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void Dispose_RevisionBumpedDuringDrain_StillCopiesCacheBackToOrigin()
  {
    _doc.Setup(d => d.Dispose()).Callback(() => _revision++);
    var host = CreateHost();

    host.Dispose();

    _fs.Operations.Should().Equal(CopyCacheToOrigin);
  }

  [Fact]
  public async Task RestoreRevisionAsync_WithoutProject_Throws()
  {
    var token = TestContext.Current.CancellationToken;
    var host = new ProjectHost(_fs, _origin, _cache);
    var revisionFile = new FileDescription(CacheDir, "version-old.gt4", null);
    _fs.AddFile(revisionFile);

    var act = () => host.RestoreRevisionAsync(
      new ProjectRevision(_fs.GetLastWriteTime(revisionFile), revisionFile), token);

    await act.Should().ThrowAsync<ApplicationException>();
  }

  [Fact]
  public async Task RestoreRevisionAsync_StaleRevisionInfo_Throws_AndKeepsProjectAttached()
  {
    var token = TestContext.Current.CancellationToken;
    var host = CreateHost();
    var revisionFile = new FileDescription(CacheDir, "version-old.gt4", null);
    _fs.AddFile(revisionFile, new DateTime(2026, 1, 1));
    var stale = new ProjectRevision(new DateTime(2026, 1, 2), revisionFile);

    var act = () => host.RestoreRevisionAsync(stale, token);

    await act.Should().ThrowAsync<ArgumentException>();
    // The failed restore must put the document back so the host stays usable.
    host.Project.Should().BeSameAs(_doc.Object);
    _doc.Verify(d => d.DisposeAsync(), Times.Never);
    _fs.Operations.Should().BeEmpty();
  }

  [Fact]
  public async Task RestoreRevisionAsync_DisposesDocument_AndCopiesRevisionToOrigin()
  {
    var token = TestContext.Current.CancellationToken;
    var host = CreateHost();
    var time = new DateTime(2026, 1, 1);
    var revisionFile = new FileDescription(CacheDir, "version-old.gt4", null);
    _fs.AddFile(revisionFile, time);

    await host.RestoreRevisionAsync(new ProjectRevision(time, revisionFile), token);

    _doc.Verify(d => d.DisposeAsync(), Times.Once);
    _fs.Operations.Should().Equal($"copy {_fs.ToPath(revisionFile)} -> {_fs.ToPath(_origin)}");
    host.Project.Should().BeNull();
    // The abandoned cache stays behind as a revision-history snapshot.
    _fs.FileExists(_cache).Should().BeTrue();
  }

  [Fact]
  public async Task RemoveRevisionAsync_RemovesTheFile()
  {
    var token = TestContext.Current.CancellationToken;
    var host = CreateHost();
    var revisionFile = new FileDescription(CacheDir, "version-old.gt4", null);
    _fs.AddFile(revisionFile);

    await host.RemoveRevisionAsync(
      new ProjectRevision(_fs.GetLastWriteTime(revisionFile), revisionFile), token);

    _fs.FileExists(revisionFile).Should().BeFalse();
  }

  [Fact]
  public async Task RemoveRevisionAsync_StaleRevisionInfo_Throws()
  {
    var token = TestContext.Current.CancellationToken;
    var host = CreateHost();
    var revisionFile = new FileDescription(CacheDir, "version-old.gt4", null);
    _fs.AddFile(revisionFile, new DateTime(2026, 1, 1));

    var act = () => host.RemoveRevisionAsync(
      new ProjectRevision(new DateTime(2026, 1, 2), revisionFile), token);

    await act.Should().ThrowAsync<ArgumentException>();
    _fs.FileExists(revisionFile).Should().BeTrue();
  }

  [Fact]
  public void Revisions_ListsSnapshots_NewestFirst_ExcludingCurrentCacheAndForeignFiles()
  {
    var host = CreateHost();
    var older = new FileDescription(CacheDir, "version-older.gt4", null);
    var newer = new FileDescription(CacheDir, "version-newer.gt4", null);
    _fs.AddFile(older, new DateTime(2026, 1, 1));
    _fs.AddFile(newer, new DateTime(2026, 2, 1));
    _fs.AddFile(new FileDescription(CacheDir, "notes.txt", null));
    _fs.AddFile(new FileDescription(OriginDir, "version-elsewhere.gt4", null));

    var revisions = host.Revisions;

    revisions.Select(r => r.FileDescription).Should().Equal(newer, older);
  }
}
