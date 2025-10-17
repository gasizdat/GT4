using System.Text.Json;

namespace GT4.Core.Utils;

internal class FileSystem : IFileSystem
{
  private void CreatePath(string path)
  {
    var parentDir = Path.GetDirectoryName(path);
    if (parentDir is not null && !Directory.Exists(parentDir))
      Directory.CreateDirectory(parentDir);
  }

  public JsonDocument ReadJsonFile(string path)
  {
    try
    {
      return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
    }
    catch
    {
      return JsonDocument.Parse("{}");
    }
  }

  public void WriteJsonFile(string path, JsonDocument doc)
  {
    try
    {
      var jsonText = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
      CreatePath(path);
      File.WriteAllText(path, jsonText);

    }
    catch (Exception ex)
    {
      throw new ApplicationException("Failed to serialize JSON document", ex);
    }
  }

  public void RemoveFile(string path)
  {
    if (File.Exists(path))
      File.Delete(path);
  }

  public void RemoveDirectory(string path)
  {
    if (Directory.Exists(path))
    {
      Directory.Delete(path, true);
    }
  }

  public FileStream CreateEmptyFile(string path)
  {
    CreatePath(path);
    return File.Create(path);
  }

  public IReadOnlyList<string> GetFiles(string path, string searchPattern, bool recursive)
  {
    if (Directory.Exists(path))
    {
      var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
      return Directory.GetFiles(path, searchPattern, option);
    }
    
    return Array.Empty<string>();
  }
}
