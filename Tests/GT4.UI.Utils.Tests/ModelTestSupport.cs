using GT4.Core.Utils;
using GT4.UI.Abstraction;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Tests;

internal sealed class FixedStorage(DirectoryDescription models) : IStorage
{
  public DirectoryDescription ProjectsCache => throw new NotSupportedException();
  public DirectoryDescription ProjectsRoot => throw new NotSupportedException();
  public DirectoryDescription AppConfig => throw new NotSupportedException();
  public DirectoryDescription Models { get; } = models;
}

// Maps every DirectoryDescription beneath a single temp root, ignoring the OS special folder it
// names -- mirrors GT4.Core.Project.Tests' DiskFileSystem, duplicated here since that one is
// internal to a different test assembly.
internal sealed class TempFileSystem(string root) : IFileSystem
{
  public string ToPath(DirectoryDescription directory) =>
    Path.Combine([root, directory.Root.ToString(), .. directory.Path]);

  public string ToPath(FileDescription file) => Path.Combine(ToPath(file.Directory), file.FileName);

  public bool FileExists(FileDescription file) => File.Exists(ToPath(file));

  public DateTime GetLastWriteTime(FileDescription file) => File.GetLastWriteTime(ToPath(file));

  public long GetFileSize(FileDescription file) => new FileInfo(ToPath(file)).Length;

  public Stream OpenWriteStream(FileDescription file)
  {
    var path = ToPath(file);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    return File.Create(path);
  }

  public Stream OpenReadStream(FileDescription file) => File.OpenRead(ToPath(file));

  public void Copy(FileDescription from, FileDescription to)
  {
    using var source = OpenReadStream(from);
    Copy(source, to);
  }

  public void Copy(Stream from, FileDescription to)
  {
    using var target = OpenWriteStream(to);
    from.CopyTo(target);
  }

  public void Move(FileDescription from, FileDescription to)
  {
    var targetPath = ToPath(to);
    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
    File.Move(ToPath(from), targetPath, overwrite: true);
  }

  public void RemoveFile(FileDescription file)
  {
    var path = ToPath(file);
    if (File.Exists(path))
    {
      File.Delete(path);
    }
  }

  public void RemoveDirectory(DirectoryDescription directory) => throw new NotSupportedException();

  public FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive)
  {
    var path = ToPath(directory);
    if (!Directory.Exists(path))
    {
      return [];
    }

    return Directory
      .GetFiles(path, searchPattern)
      .Select(Path.GetFileName)
      .Select(fileName => new FileDescription(directory, fileName!, null))
      .ToArray();
  }
}
