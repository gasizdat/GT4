namespace GT4.Core.Utils;

public static class FileNameUtils
{
  // The Windows invalid-filename set, a superset of every other platform's. Spelled out rather than taken
  // from Path.GetInvalidFileNameChars(), which on Unix is only '\0' and '/' -- a name produced here can
  // travel to another machine, so it has to be valid everywhere regardless of where it was made.
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
