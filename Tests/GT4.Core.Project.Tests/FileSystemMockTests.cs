using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public class FileSystemMockTests
{
  private static readonly DirectoryDescription Directory = new(Environment.SpecialFolder.ApplicationData, ["dir"]);

  [Fact]
  public void Move_RelocatesFileAndRemovesSource()
  {
    var mock = new FileSystemMock();
    var from = new FileDescription(Directory, "source.bin", null);
    var to = new FileDescription(Directory, "target.bin", null);
    mock.AddFile(from);

    mock.Move(from, to);

    Assert.False(mock.FileExists(from));
    Assert.True(mock.FileExists(to));
    Assert.Contains($"move {mock.ToPath(from)} -> {mock.ToPath(to)}", mock.Operations);
  }

  [Fact]
  public void Move_MissingSource_Throws()
  {
    var mock = new FileSystemMock();
    var from = new FileDescription(Directory, "source.bin", null);
    var to = new FileDescription(Directory, "target.bin", null);

    Assert.Throws<FileNotFoundException>(() => mock.Move(from, to));
  }
}
