using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Covers <see cref="CurrentProjectProvider"/>: the open/close lifecycle, the "not opened" guards on
/// the accessors, and the revision restore/remove delegation. Backed by a real <see cref="ProjectHost"/>
/// over the in-memory <see cref="FileSystemMock"/> with a mocked <see cref="IProjectDocument"/>.
/// </summary>
public sealed class CurrentProjectProviderTests
{
  private static readonly DirectoryDescription Dir = new(Environment.SpecialFolder.MyDocuments, ["projects"]);

  private readonly FileSystemMock _fs = new();
  private readonly FileDescription _origin = new(Dir, "family.gt4", null);
  private readonly FileDescription _cache = new(Dir, "version-current.gt4", null);
  private readonly Mock<IProjectList> _list = new(MockBehavior.Strict);
  private readonly ProjectInfo _info;

  public CurrentProjectProviderTests()
  {
    _fs.AddFile(_origin);
    _info = new ProjectInfo("Tree", "Desc", 1L, _origin);

    _list
      .Setup(l => l.OpenAsync(It.IsAny<FileDescription>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((FileDescription origin, CancellationToken _) => CreateHost());
  }

  private ProjectHost CreateHost()
  {
    var doc = new Mock<IProjectDocument>(MockBehavior.Loose);
    doc.SetupGet(d => d.ProjectRevision).Returns(1L);
    doc.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);
    doc.Setup(d => d.Dispose());
    var host = new ProjectHost(_fs, _origin, _cache) { Project = doc.Object };
    return host;
  }

  private CancellationToken Token => TestContext.Current.CancellationToken;

  [Fact]
  public void BeforeOpen_Accessors_Throw()
  {
    var provider = new CurrentProjectProvider(_list.Object);

    provider.HasCurrentProject.Should().BeFalse();
    provider.Invoking(p => _ = p.Project).Should().Throw<InvalidOperationException>();
    provider.Invoking(p => _ = p.Info).Should().Throw<InvalidOperationException>();
    provider.Invoking(p => _ = p.Revisions).Should().Throw<InvalidOperationException>();
  }

  [Fact]
  public async Task Open_MakesProjectCurrent()
  {
    var provider = new CurrentProjectProvider(_list.Object);

    await provider.OpenAsync(_info, Token);

    provider.HasCurrentProject.Should().BeTrue();
    provider.Project.Should().NotBeNull();
    provider.Info.Should().Be(_info);
    provider.Revisions.Should().NotBeNull();
    _list.Verify(l => l.OpenAsync(_origin, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Open_WhenAlreadyOpen_ClosesPrevious()
  {
    var provider = new CurrentProjectProvider(_list.Object);

    await provider.OpenAsync(_info, Token);
    await provider.OpenAsync(_info, Token);

    // Each open opens a host; the second open first closed the first one.
    _list.Verify(l => l.OpenAsync(It.IsAny<FileDescription>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    provider.HasCurrentProject.Should().BeTrue();
  }

  [Fact]
  public async Task Close_WithoutOpen_IsNoOp()
  {
    var provider = new CurrentProjectProvider(_list.Object);

    await provider.CloseAsync(Token);

    provider.HasCurrentProject.Should().BeFalse();
  }

  [Fact]
  public async Task Close_AfterOpen_ClearsCurrentProject()
  {
    var provider = new CurrentProjectProvider(_list.Object);
    await provider.OpenAsync(_info, Token);

    await provider.CloseAsync(Token);

    provider.HasCurrentProject.Should().BeFalse();
  }

  [Fact]
  public async Task UpdateOrigin_ReopensSameInfo()
  {
    var provider = new CurrentProjectProvider(_list.Object);
    await provider.OpenAsync(_info, Token);

    await provider.UpdateOriginAsync(Token);

    provider.HasCurrentProject.Should().BeTrue();
    provider.Info.Should().Be(_info);
    _list.Verify(l => l.OpenAsync(It.IsAny<FileDescription>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public async Task RemoveRevision_WithoutOpen_Throws()
  {
    var provider = new CurrentProjectProvider(_list.Object);
    var revision = new ProjectRevision(DateTime.UtcNow, new FileDescription(Dir, "version-old.gt4", null));

    var act = () => provider.RemoveRevisionAsync(revision, Token);

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task RemoveRevision_AfterOpen_RemovesFile()
  {
    var provider = new CurrentProjectProvider(_list.Object);
    await provider.OpenAsync(_info, Token);
    var revisionFile = new FileDescription(Dir, "version-old.gt4", null);
    _fs.AddFile(revisionFile, new DateTime(2026, 1, 1));

    await provider.RemoveRevisionAsync(
      new ProjectRevision(_fs.GetLastWriteTime(revisionFile), revisionFile), Token);

    _fs.FileExists(revisionFile).Should().BeFalse();
  }

  [Fact]
  public async Task RestoreRevision_WithoutOpen_Throws()
  {
    var provider = new CurrentProjectProvider(_list.Object);
    var revision = new ProjectRevision(DateTime.UtcNow, new FileDescription(Dir, "version-old.gt4", null));

    var act = () => provider.RestoreRevisionAsync(revision, Token);

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task RestoreRevision_CopiesRevisionToOrigin_AndReopens()
  {
    var provider = new CurrentProjectProvider(_list.Object);
    await provider.OpenAsync(_info, Token);
    var time = new DateTime(2026, 1, 1);
    var revisionFile = new FileDescription(Dir, "version-old.gt4", null);
    _fs.AddFile(revisionFile, time);
    _fs.ResetStats();

    await provider.RestoreRevisionAsync(new ProjectRevision(time, revisionFile), Token);

    _fs.Operations.Should().Contain($"copy {_fs.ToPath(revisionFile)} -> {_fs.ToPath(_origin)}");
    provider.HasCurrentProject.Should().BeTrue();
    // Opened initially, then reopened after the restore.
    _list.Verify(l => l.OpenAsync(It.IsAny<FileDescription>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
  }
}
