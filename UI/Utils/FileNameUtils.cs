namespace GT4.UI.Utils;

public static class FileNameUtils
{
  // Strips every character invalid in a Windows/Android/iOS file name -- including path separators and
  // drive letters -- so a GEDCOM FILE value (often a full original path) can't escape the directory it's
  // written into.
  public static string Sanitize(string name, string fallback)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
  }
}
