using System.Text.Json;

namespace GT4.Utils;

public interface IFileSystem
{
  JsonDocument ReadJsonFile(string path);
  void WriteJsonFile(string path, JsonDocument doc);
  void RemoveFile(string path);
  FileStream CreateEmptyFile(string path);
  IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recursive);
}