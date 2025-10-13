using System.Text.Json;

namespace GT4.Utils;

internal class FileSystem : IFileSystem
{
  private static void CreatePath(string path)
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
}
