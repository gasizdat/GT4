namespace GT4.UI.Utils;

public static class FileNameUtils
{
  // Replacing invalid characters keeps the result from escaping the directory it's later combined into.
  public static string Sanitize(string name, string fallback)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
  }
}
