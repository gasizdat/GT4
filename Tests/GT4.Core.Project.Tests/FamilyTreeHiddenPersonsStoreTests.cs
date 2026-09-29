using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class FamilyTreeHiddenPersonsStoreTests
{
  private static (FamilyTreeHiddenPersonsStore Store, string Root) NewStore()
  {
    var root = Path.Combine(Path.GetTempPath(), $"gt4_hiddenpersons_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var factory = new ProjectConfigurationProvider.Factory(new DiskFileSystem(root), new TempStorage());
    return (new FamilyTreeHiddenPersonsStore(factory), root);
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
      store.Get(Project("sample.gt4")).Should().BeEmpty();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void SetThenGet_RoundTripsIds()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, [3, 7]);

      store.Get(project).Should().BeEquivalentTo([3, 7]);
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void SetEmpty_ShowsEveryoneAgain()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, [3]);

      store.Set(project, []);

      store.Get(project).Should().BeEmpty();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void DistinctOrigins_KeepDistinctSets()
  {
    var (store, root) = NewStore();
    try
    {
      store.Set(Project("a.gt4"), [1]);

      store.Get(Project("b.gt4")).Should().BeEmpty();
    }
    finally { Directory.Delete(root, true); }
  }
}
