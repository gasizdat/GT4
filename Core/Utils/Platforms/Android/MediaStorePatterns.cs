namespace GT4.Core.Utils;

// Pure string logic behind AndroidFileSystem's MediaStore queries, kept free of Android types
// (and outside its #if ANDROID) so it stays unit-testable on any host.
internal static class MediaStorePatterns
{
  public const string PathSeparator = "/";

  public static string EnsureTrailingSlash(string path) =>
    string.IsNullOrEmpty(path) ? path : (path.EndsWith(PathSeparator) ? path : path + PathSeparator);

  public static string ToLikePattern(string path)
  {
    // turn simple wildcards into SQL LIKE, escaping others
    var escaped = EscapeForLike(path);
    return escaped.Replace("*", "%").Replace("?", "_");
  }

  // Escape % and _ (special in LIKE) and backslashes; keep slashes as-is.
  private static string EscapeForLike(string path) =>
    path.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
