namespace GT4.Core.Utils;

public static class FileNameUtils
{
  // The printable half of the Windows invalid-filename set; Sanitize adds the control characters below it,
  // and the two together are exactly what Path.GetInvalidFileNameChars() returns there. Spelled out rather
  // than taken from that call, which on Unix returns only '\0' and '/' -- a name produced here can travel
  // to another machine, so it has to be valid everywhere regardless of where it was made.
  private const string InvalidChars = "<>:\"/\\|?*";

  /// <summary>
  /// Makes a name safe to use as a single filename: characters no file system accepts become underscores,
  /// and a name left with nothing but dots or blanks falls back rather than becoming a traversal segment.
  /// Path separators are among the replaced characters, so the result can never escape the directory it is
  /// later combined into.
  /// </summary>
  public static string Sanitize(string name, string fallback)
  {
    var sanitized = new string([.. name.Select(c => c < ' ' || InvalidChars.Contains(c) ? '_' : c)]);
    return sanitized.Trim().Trim('.').Length == 0 ? fallback : sanitized;
  }
}
