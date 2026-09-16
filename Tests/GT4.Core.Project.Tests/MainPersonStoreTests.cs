using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class MainPersonStoreTests
{
  private static (MainPersonStore Store, string Root) NewStore()
  {
    var root = Path.Combine(Path.GetTempPath(), $"gt4_mainperson_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var factory = new ProjectConfigurationProvider.Factory(new DiskFileSystem(root), new TempStorage());
    return (new MainPersonStore(factory), root);
  }

  private static ProjectInfo Project(string fileName) => new(
    Name: fileName,
    Description: string.Empty,
    Revision: null,
    Origin: new FileDescription(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, ["a", "b"]), fileName, null));

  [Fact]
  public void Get_WhenNothingStored_ReturnsNull()
  {
    var (store, root) = NewStore();
    try
    {
      store.Get(Project("sample.gt4")).Should().BeNull();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void SetThenGet_RoundTripsIdAndDisplayName()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, 42, "Ada Lovelace");

      store.Get(project).Should().Be(new MainPersonInfo(42, "Ada Lovelace"));
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void Clear_RemovesTheStoredMainPerson()
  {
    var (store, root) = NewStore();
    try
    {
      var project = Project("sample.gt4");
      store.Set(project, 1, "Someone");

      store.Clear(project);

      store.Get(project).Should().BeNull();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void DistinctOrigins_DeriveDistinctKeys()
  {
    var (store, root) = NewStore();
    try
    {
      store.Set(Project("a.gt4"), 1, "A");
      store.Set(Project("b.gt4"), 2, "B");

      store.Get(Project("a.gt4")).Should().Be(new MainPersonInfo(1, "A"));
      store.Get(Project("b.gt4")).Should().Be(new MainPersonInfo(2, "B"));
    }
    finally { Directory.Delete(root, true); }
  }
}
