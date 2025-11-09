namespace GT4.Core.Utils;

internal class FileSystem : IFileSystem
{
  private static void CreatePath(string path)
  {
    var parentDir = Path.GetDirectoryName(path);
    if (parentDir is not null && !Directory.Exists(parentDir))
      Directory.CreateDirectory(parentDir);
  }

  private FileDescription ToFileDescription(DirectoryDescription baseDir, string path)
  {
    var basePath = ToPath(baseDir);
    var relativePath = Path.GetRelativePath(basePath, path);
    var relativeDirs = Path.GetDirectoryName(relativePath)?.Split(Path.PathSeparator) ?? [];
    var directory = baseDir with { Path = baseDir.Path.Concat(relativeDirs).ToArray() };

    return new FileDescription(
      Directory: directory,
      Path.GetFileName(relativePath),
      MimeType: null // TODO
    );
  }

  public string ToPath(DirectoryDescription directoryDescription)
  {
    return Path.Combine(
      Environment.GetFolderPath(directoryDescription.Root),
      Path.Combine(directoryDescription.Path));
  }

  public string ToPath(FileDescription fileDescription)
  {
    return Path.Combine(ToPath(fileDescription.Directory), fileDescription.FileName);
  }

  public void RemoveFile(FileDescription fileDescription)
  {
    var path = ToPath(fileDescription);
    if (File.Exists(path))
    {
      File.Delete(path);
    }
  }

  public void RemoveDirectory(DirectoryDescription directoryDescription)
  {
    var path = ToPath(directoryDescription);

    if (Directory.Exists(path))
    {
      Directory.Delete(path, true);
    }
  }

  public Stream OpenWriteStream(FileDescription fileDescription)
  {
    var path = ToPath(fileDescription);
    CreatePath(path);
    return File.OpenWrite(path);
  }

  public Stream OpenReadStream(FileDescription fileDescription)
  {
    var path = ToPath(fileDescription);
    return File.OpenRead(path);
  }

  public FileDescription[] GetFiles(DirectoryDescription directoryDescription, string searchPattern, bool recursive)
  {
    var path = ToPath(directoryDescription);
    if (Directory.Exists(path))
    {
      var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
      return Directory
        .GetFiles(path, searchPattern, option)
        .Select(filePath => ToFileDescription(directoryDescription, filePath)).ToArray();
    }

    return Array.Empty<FileDescription>();
  }

  public void Copy(FileDescription from, FileDescription to)
  {
    using var sourceStream = OpenReadStream(from);
    Copy(sourceStream, to);
  }

  public void Copy(Stream from, FileDescription to)
  {
    using var targetStream = OpenWriteStream(to);
    from.CopyTo(targetStream);
    targetStream.Flush();
    targetStream.Close();
  }

  public bool FileExists(FileDescription FileExists)
  {
    return File.Exists(ToPath(FileExists));
  }
}
