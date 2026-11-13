using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class FileSystemTests : IDisposable
{
  private readonly DirectoryDescription _Root =
    new(Environment.SpecialFolder.LocalApplicationData, [$"GT4.FileSystemTests.{Guid.NewGuid():N}"]);
  private readonly FileSystem _FileSystem = new();

  public void Dispose() => _FileSystem.RemoveDirectory(_Root);

  [Fact]
  public void Move_IntoAMissingDirectory_CreatesItAndRemovesTheSource()
  {
    var from = new FileDescription(_Root, "model.tmp", null);
    var to = new FileDescription(_Root with { Path = [.. _Root.Path, "models"] }, "model.onnx", null);
    Write(from, [1, 2, 3]);

    _FileSystem.Move(from, to);

    Assert.False(_FileSystem.FileExists(from));
    Assert.Equal([1, 2, 3], Read(to));
  }

  [Fact]
  public void Move_OntoAnExistingFile_ReplacesIt()
  {
    var from = new FileDescription(_Root, "model.tmp", null);
    var to = new FileDescription(_Root, "model.onnx", null);
    Write(from, [1, 2, 3]);
    Write(to, [9, 9]);

    _FileSystem.Move(from, to);

    Assert.False(_FileSystem.FileExists(from));
    Assert.Equal([1, 2, 3], Read(to));
  }

  private void Write(FileDescription file, byte[] content)
  {
    using var stream = _FileSystem.OpenWriteStream(file);
    stream.Write(content);
  }

  private byte[] Read(FileDescription file)
  {
    var path = _FileSystem.ToPath(file);
    return File.ReadAllBytes(path);
  }
}
