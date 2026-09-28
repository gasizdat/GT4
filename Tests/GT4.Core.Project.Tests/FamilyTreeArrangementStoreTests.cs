using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class FamilyTreeArrangementStoreTests
{
  private static (FamilyTreeArrangementStore Store, string Root) NewStore()
  {
    var root = Path.Combine(Path.GetTempPath(), $"gt4_arrangement_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var factory = new ProjectConfigurationProvider.Factory(new DiskFileSystem(root), new TempStorage());
    return (new FamilyTreeArrangementStore(factory), root);
  }

  private static ProjectInfo Project(string fileName) => new(
    Name: fileName,
    Description: string.Empty,
    Revision: null,
    Origin: new FileDescription(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, ["a", "b"]), fileName, null));

  [Fact]
  public void Get_WhenNothingStored_ReturnsEmpty()
  {
    var (store, root) = NewStore();
    try
    {
      store.Get(Project("sample.gt4"), 1).Should().BeEmpty();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void SetThenGet_RoundTripsOffsets()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, 1, new Dictionary<int, double> { [2] = -1.5, [3] = 2.25 });

      store.Get(project, 1).Should().BeEquivalentTo(new Dictionary<int, double> { [2] = -1.5, [3] = 2.25 });
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void EachCentre_KeepsItsOwnArrangement()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, 1, new Dictionary<int, double> { [2] = 1 });
      store.Set(project, 5, new Dictionary<int, double> { [2] = -3 });

      store.Get(project, 1).Should().BeEquivalentTo(new Dictionary<int, double> { [2] = 1 });
      store.Get(project, 5).Should().BeEquivalentTo(new Dictionary<int, double> { [2] = -3 });
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void Clear_RemovesOnlyThatCentresArrangement()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, 1, new Dictionary<int, double> { [2] = 1 });
      store.Set(project, 5, new Dictionary<int, double> { [2] = -3 });

      store.Clear(project, 1);

      store.Get(project, 1).Should().BeEmpty();
      store.Get(project, 5).Should().BeEquivalentTo(new Dictionary<int, double> { [2] = -3 });
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void DistinctOrigins_KeepDistinctArrangements()
  {
    var (store, root) = NewStore();
    try
    {
      store.Set(Project("a.gt4"), 1, new Dictionary<int, double> { [2] = 1 });

      store.Get(Project("b.gt4"), 1).Should().BeEmpty();
    }
    finally { Directory.Delete(root, true); }
  }
}
