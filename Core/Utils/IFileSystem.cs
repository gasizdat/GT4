namespace GT4.Core.Utils;

public interface IFileSystem
{
  void RemoveFile(FileDescription fileDescription);
  Stream OpenWriteStream(FileDescription fileDescription);
  Stream OpenReadStream(FileDescription fileDescription);
  void Copy(FileDescription from, FileDescription to);
  FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive);
  void RemoveDirectory(DirectoryDescription directory);

  string ToPath(DirectoryDescription fileDescription);
  string ToPath(FileDescription fileDescription);
}