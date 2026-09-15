using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class MainPersonStoreTests
{
  private static (MainPersonStore Store, string Root) NewStore()
  {
    var root = Path.Combine(Path.GetTempPath(), $"gt4_mainperson_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var provider = new AppConfigurationProvider(new DiskFileSystem(root), new TempStorage());
    var configurationRoot = new ConfigurationRoot(new IConfigurationProvider[] { provider });
    return (new MainPersonStore(configurationRoot, provider), root);
  }

  private static FileDescription Origin(string fileName) =>
    new(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, ["a", "b"]), fileName, null);

  [Fact]
  public void Get_WhenNothingStored_ReturnsNull()
  {
    var (store, root) = NewStore();
    try
    {
      store.Get(Origin("sample.gt4")).Should().BeNull();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void SetThenGet_RoundTripsIdAndDisplayName()
  {
    var (store, root) = NewStore();
    try
    {
      var origin = Origin("sample.gt4");
      store.Set(origin, 42, "Ada Lovelace");

      store.Get(origin).Should().Be(new MainPersonInfo(42, "Ada Lovelace"));
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void Clear_RemovesTheStoredMainPerson()
  {
    var (store, root) = NewStore();
    try
    {
      var origin = Origin("sample.gt4");
      store.Set(origin, 1, "Someone");

      store.Clear(origin);

      store.Get(origin).Should().BeNull();
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void DistinctOrigins_DeriveDistinctKeys()
  {
    var (store, root) = NewStore();
    try
    {
      store.Set(Origin("a.gt4"), 1, "A");
      store.Set(Origin("b.gt4"), 2, "B");

      store.Get(Origin("a.gt4")).Should().Be(new MainPersonInfo(1, "A"));
      store.Get(Origin("b.gt4")).Should().Be(new MainPersonInfo(2, "B"));
    }
    finally { Directory.Delete(root, true); }
  }
}
