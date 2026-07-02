using FluentAssertions;
using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Logic;
using Moq;
using Xunit;

namespace GT4.UI.Logic.Tests;

using IFileSystem = GT4.Core.Utils.IFileSystem;

public sealed class ProjectListLogicTests
{
  private readonly Mock<IProjectList> _projectList = new();
  private readonly Mock<ICurrentProjectProvider> _currentProjectProvider = new();
  private readonly Mock<IComparer<ProjectInfo>> _comparer = new();
  private readonly Mock<IGedcomImporter> _importer = new();
  private readonly Mock<ICancellationTokenProvider> _cancellationTokenProvider = new();

  public ProjectListLogicTests()
  {
    _cancellationTokenProvider.Setup(p => p.CreateDbCancellationToken())
                              .Returns(() => new CancellationTokenHost(TimeSpan.FromSeconds(5)));
  }

  private ProjectListLogic CreateLogic() => new(
    _projectList.Object,
    _currentProjectProvider.Object,
    _comparer.Object,
    _importer.Object,
    _cancellationTokenProvider.Object);

  private static ProjectInfo MakeInfo(string name) =>
    new(Name: name, Description: string.Empty, Revision: string.Empty,
        Origin: new FileDescription(new DirectoryDescription(Environment.SpecialFolder.LocalApplicationData, []), name, null));

  [Fact]
  public async Task GetProjectsAsync_returns_projects_sorted_by_comparer()
  {
    var a = MakeInfo("Alpha");
    var b = MakeInfo("Beta");
    var c = MakeInfo("Gamma");
    _projectList.Setup(l => l.GetItemsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([c, a, b]);
    _comparer.Setup(cmp => cmp.Compare(It.IsAny<ProjectInfo>(), It.IsAny<ProjectInfo>()))
             .Returns((ProjectInfo x, ProjectInfo y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));

    var result = await CreateLogic().GetProjectsAsync();

    result.Should().ContainInOrder(a, b, c);
  }

  [Fact]
  public async Task ImportAsync_removes_project_and_rethrows_when_import_fails()
  {
    var fsm = new Mock<IFileSystem>();
    var dir = new DirectoryDescription(Environment.SpecialFolder.LocalApplicationData, ["gt4_test"]);
    var origin = new FileDescription(dir, "project.db", null);
    var cache = new FileDescription(dir, "project.cache.db", null);
    var host = new ProjectHost(fsm.Object, origin, cache);

    _projectList.Setup(l => l.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(host);
    _importer.Setup(i => i.ImportAsync(It.IsAny<IProjectDocument>(), It.IsAny<TextReader>(),
                                        It.IsAny<CancellationToken>(), It.IsAny<string?>()))
             .ThrowsAsync(new InvalidOperationException("bad file"));

    var logic = CreateLogic();
    var act = () => logic.ImportAsync(new MemoryStream(), "Test", "desc", null, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>();
    _projectList.Verify(l => l.RemoveAsync(origin, It.IsAny<CancellationToken>()), Times.Once);
  }
}
