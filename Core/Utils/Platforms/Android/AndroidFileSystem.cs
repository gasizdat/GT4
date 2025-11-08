using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidApplication = Android.App.Application;

namespace GT4.Core.Utils;

public class AndroidFileSystem : IFileSystem
{
  const string AndroidPathSeparator = "/";
  private readonly FileSystem _DirectAccessFileSystem = new();

  private static string GetAndroidRoot(DirectoryDescription directoryDescription)
  {
    return directoryDescription.Root switch
    {
      System.Environment.SpecialFolder.MyDocuments => Android.OS.Environment.DirectoryDocuments
        ?? throw new IOException($"{nameof(Android.OS.Environment.DirectoryDocuments)}"),

      _ => throw new NotSupportedException($"Not Supported root: {directoryDescription.Root}")
    };
  }

  private static string GetRelativePath(DirectoryDescription directoryDescription)
  {
    var chanks = new List<string>([GetAndroidRoot(directoryDescription)]);
    chanks.AddRange(directoryDescription.Path);
    var ret = Path.Combine(chanks.ToArray());

    return ret;
  }

  private static bool IsInternalStorage(DirectoryDescription directoryDescription) =>
    directoryDescription.Root == System.Environment.SpecialFolder.ApplicationData;

  private static string EnsureTrailingSlash(string path) =>
    string.IsNullOrEmpty(path) ? path : (path.EndsWith(AndroidPathSeparator) ? path : path + AndroidPathSeparator);

  // Escape % and _ (special in LIKE) and backslashes; keep slashes as-is.
  private static string EscapeForLike(string path) =>
    path.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

  private static string ToLikePattern(string path)
  {
    // turn simple wildcards into SQL LIKE, escaping others
    var escaped = EscapeForLike(path);
    return escaped.Replace("*", "%").Replace("?", "_");
  }

  private static bool ResourceExists(Android.Net.Uri uri)
  {
    try
    {
      using var stream = AndroidApplication.Context.ContentResolver?.OpenInputStream(uri);
      stream?.Close();
      return stream is not null;
    }
    catch (Java.IO.FileNotFoundException)
    {
      return false;
    }
  }

  private static Dictionary<FileDescription, Android.Net.Uri> GetFilesUri(DirectoryDescription directoryDescription, string searchPattern, bool recursive)
  {
    var ret = new Dictionary<FileDescription, Android.Net.Uri>();
    var externalStorageUri = GetExternalStorageUri();
    var query = new List<string>();
    var args = new List<string>();
    var sort = $"{MediaStore.IMediaColumns.DateModified} DESC";
    string[] projection = 
    [
        IBaseColumns.Id,
        MediaStore.IMediaColumns.DisplayName,
        MediaStore.IMediaColumns.MimeType,
        MediaStore.IMediaColumns.RelativePath,
        MediaStore.IMediaColumns.DateModified
    ];

    if (recursive)
    {
      query.Add($"{MediaStore.IMediaColumns.RelativePath} LIKE ? ESCAPE '\\'");
      args.Add($"{EnsureTrailingSlash(GetRelativePath(directoryDescription))}%");
    }
    else
    {
      query.Add($"{MediaStore.IMediaColumns.RelativePath}=?");
      args.Add(EnsureTrailingSlash(GetRelativePath(directoryDescription)));
    }

    if (!string.IsNullOrWhiteSpace(searchPattern) && searchPattern != "*")
    {
      query.Add($"{MediaStore.IMediaColumns.DisplayName} LIKE ? ESCAPE '\\'");
      args.Add(ToLikePattern(searchPattern));
    }

    // NEW: Exclude pending & trashed
    if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // API 30+
    {
      query.Add($"{MediaStore.IMediaColumns.IsTrashed}=?");
      args.Add("0");

      // Optional: Exclude items scheduled for purge from Trash
      // (DATE_EXPIRES is usually set for trashed items; use if you prefer)
      query.Add($"{MediaStore.IMediaColumns.DateExpires} IS NULL");
      args.Add("0");
    }
    else if (Build.VERSION.SdkInt >= BuildVersionCodes.Q) // API 29+
    {
      query.Add($"{MediaStore.IMediaColumns.IsPending}=?");
      args.Add("0");
    }

    using var cursor = AndroidApplication.Context.ContentResolver?.Query(
      externalStorageUri,
      projection,
      string.Join(" AND ", query),
      args.ToArray(),
      sort);

    while (cursor?.MoveToNext() == true)
    {
      var documentId = cursor.GetLong(cursor.GetColumnIndexOrThrow(Android.Provider.IBaseColumns.Id));
      var displayName = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.DisplayName));
      var mimeType = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.MimeType));
      var relativePath = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.RelativePath));
      var uri = ContentUris.WithAppendedId(externalStorageUri, documentId);

      if (!ResourceExists(uri))
      {
        continue;
      }

      IEnumerable<string> chunks = relativePath?.Split(AndroidPathSeparator) ?? [];
      if (chunks.FirstOrDefault() == GetAndroidRoot(directoryDescription))
      {
        chunks = chunks.Skip(1);
      }
      if (chunks.LastOrDefault() == string.Empty)
      {
        chunks = chunks.SkipLast(1);
      }

      var fileDescription = new FileDescription(
        Directory: new DirectoryDescription(
          Root: directoryDescription.Root,
          Path: chunks.ToArray()),
        FileName: displayName ?? "",
        MimeType: mimeType);

      ret[fileDescription] = uri;
    }

    return ret;
  }

  private static Android.Net.Uri GetExternalStorageUri()
  {
    return MediaStore.Files.GetContentUri(MediaStore.VolumeExternalPrimary)
      ?? throw new IOException("Cannot get external URI");
  }

  public AndroidFileSystem()
  {
    if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
    {
      throw new NotSupportedException($"API < 29+ is not supported. SDK Version: {Build.VERSION.SdkInt}");
    }
  }

  public string ToPath(DirectoryDescription directoryDescription)
  {
    if (!IsInternalStorage(directoryDescription))
    {
      throw new ArgumentException($"{nameof(directoryDescription)} should be internal");
    }
    return _DirectAccessFileSystem.ToPath(directoryDescription);
  }

  public string ToPath(FileDescription fileDescription)
  {
    if (!IsInternalStorage(fileDescription.Directory))
    {
      throw new ArgumentException($"{nameof(fileDescription)} should be internal");
    }
    return _DirectAccessFileSystem.ToPath(fileDescription);
  }

  public void RemoveFile(FileDescription fileDescription)
  {
    if (IsInternalStorage(fileDescription.Directory))
    {
      _DirectAccessFileSystem.RemoveFile(fileDescription);
      return;
    }

    throw new NotImplementedException();
  }

  public void RemoveDirectory(DirectoryDescription directoryDescription)
  {
    if (directoryDescription.Root == System.Environment.SpecialFolder.ApplicationData)
    {
      _DirectAccessFileSystem.RemoveDirectory(directoryDescription);
    }

    throw new NotImplementedException();
  }

  public Stream OpenWriteStream(FileDescription fileDescription)
  {
    if (IsInternalStorage(fileDescription.Directory))
    {
      return _DirectAccessFileSystem.OpenWriteStream(fileDescription);
    }

    Stream outStream;

    var uris = GetFilesUri(fileDescription.Directory, string.Empty, false);
    if (uris.TryGetValue(fileDescription, out var uri))
    {
      outStream = AndroidApplication.Context.ContentResolver?.OpenOutputStream(uri, "wt")
                    ?? throw new IOException("Failed to open output stream to write.");
    }
    else
    {
      using var values = new ContentValues();
      values.Put(MediaStore.IMediaColumns.DisplayName, fileDescription.FileName);
      values.Put(MediaStore.IMediaColumns.MimeType, fileDescription.MimeType);
      values.Put(MediaStore.IMediaColumns.RelativePath, GetRelativePath(fileDescription.Directory));

      var externaStoragelUri = GetExternalStorageUri();
      uri = AndroidApplication.Context.ContentResolver?.Insert(externaStoragelUri, values)
            ?? throw new IOException("Failed to create file via MediaStore.");
      outStream = AndroidApplication.Context.ContentResolver?.OpenOutputStream(uri, "wt")
                  ?? throw new IOException("Failed to open output stream to write.");
    }

    return outStream;
  }

  public Stream OpenReadStream(FileDescription fileDescription)
  {
    if (IsInternalStorage(fileDescription.Directory))
    {
      return _DirectAccessFileSystem.OpenReadStream(fileDescription);
    }

    var uris = GetFilesUri(fileDescription.Directory, string.Empty, false);
    if (!uris.TryGetValue(fileDescription, out var uri))
    {
      throw new IOException($"The file {fileDescription} doesn't exist");
    }

    var outStream = AndroidApplication.Context.ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Failed to open output stream to read.");
    return outStream;
  }

  public FileDescription[] GetFiles(DirectoryDescription directoryDescription, string searchPattern, bool recursive)
  {
    if (directoryDescription.Root == System.Environment.SpecialFolder.ApplicationData)
    {
      return _DirectAccessFileSystem.GetFiles(directoryDescription, searchPattern, recursive);
    }

    return GetFilesUri(directoryDescription, searchPattern, recursive).Keys.ToArray();
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

  public bool FileExists(FileDescription fileDescription)
  {
    if (IsInternalStorage(fileDescription.Directory))
    {
      return _DirectAccessFileSystem.FileExists(fileDescription);
    }
    var uris = GetFilesUri(fileDescription.Directory, string.Empty, false);
    
    var ret = uris.TryGetValue(fileDescription, out var uri) && uri is not null;
    return ret;
  }
}
