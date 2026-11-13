using GT4.Core.Utils;

namespace GT4.Core.Project.Tests;

using IFileSystem = GT4.Core.Utils.IFileSystem;

/// <summary>
/// A real-disk <see cref="IFileSystem"/> that maps every <see cref="DirectoryDescription"/> beneath a
/// single temp root, so tests get genuine file/SQLite behaviour without writing to the user's folders.
/// </summary>
/// <param name="fileSize">Overrides the reported size, so a test can drive the sweep's size grouping
/// independently of what the files on disk actually weigh.</param>
internal sealed class DiskFileSystem(string root, Func<FileDescription, long>? fileSize = null) : IFileSystem
{
  public string ToPath(DirectoryDescription directory) =>
    Path.Combine(new[] { root, directory.Root.ToString() }.Concat(directory.Path).ToArray());

  public string ToPath(FileDescription file) => Path.Combine(ToPath(file.Directory), file.FileName);

  public bool FileExists(FileDescription file) => File.Exists(ToPath(file));

  public DateTime GetLastWriteTime(FileDescription file) => File.GetLastWriteTime(ToPath(file));

  public long GetFileSize(FileDescription file) => fileSize?.Invoke(file) ?? new FileInfo(ToPath(file)).Length;

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
    if (File.Exists(path)) File.Delete(path);
  }

  public void RemoveDirectory(DirectoryDescription directory)
  {
    var path = ToPath(directory);
    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
  }

  public FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive)
  {
    var basePath = ToPath(directory);
    if (!Directory.Exists(basePath)) return [];

    var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    return Directory
      .GetFiles(basePath, searchPattern, option)
      .Select(fp =>
      {
        var relative = Path.GetRelativePath(basePath, fp);
        var relativeDir = Path.GetDirectoryName(relative);
        var subdirs = string.IsNullOrEmpty(relativeDir)
          ? Array.Empty<string>()
          : relativeDir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return new FileDescription(directory with { Path = [.. directory.Path, .. subdirs] }, Path.GetFileName(relative), null);
      })
      .ToArray();
  }
}
