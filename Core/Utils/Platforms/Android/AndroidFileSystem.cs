#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidApplication = Android.App.Application;

namespace GT4.Core.Utils;

public class AndroidFileSystem : IFileSystem
{
  private readonly IFileSystem _DirectAccessFileSystem = new FileSystem();
  private readonly IFileSystem _MediaStoreFileSystem = new MediaStoreFileSystem();

  public AndroidFileSystem()
  {
    if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
    {
      throw new NotSupportedException($"API < 29+ is not supported. SDK Version: {Build.VERSION.SdkInt}");
    }
  }

  public string ToPath(DirectoryDescription directoryDescription) =>
    Select(directoryDescription).ToPath(directoryDescription);

  public string ToPath(FileDescription fileDescription) =>
    Select(fileDescription.Directory).ToPath(fileDescription);

  public void RemoveFile(FileDescription fileDescription) =>
    Select(fileDescription.Directory).RemoveFile(fileDescription);

  public void RemoveDirectory(DirectoryDescription directoryDescription) =>
    Select(directoryDescription).RemoveDirectory(directoryDescription);

  public Stream OpenWriteStream(FileDescription fileDescription) =>
    Select(fileDescription.Directory).OpenWriteStream(fileDescription);

  public Stream OpenReadStream(FileDescription fileDescription) =>
    Select(fileDescription.Directory).OpenReadStream(fileDescription);

  public FileDescription[] GetFiles(DirectoryDescription directoryDescription, string searchPattern, bool recursive) =>
    Select(directoryDescription).GetFiles(directoryDescription, searchPattern, recursive);

  // Copies can cross storages (external origin <-> internal cache), so both overloads are composed
  // here from the per-argument stream operations and never dispatched whole to a single storage.
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

  public bool FileExists(FileDescription fileDescription) =>
    Select(fileDescription.Directory).FileExists(fileDescription);

  public DateTime GetLastWriteTime(FileDescription fileDescription) =>
    Select(fileDescription.Directory).GetLastWriteTime(fileDescription);

  private IFileSystem Select(DirectoryDescription directoryDescription) =>
    IsInternalStorage(directoryDescription) ? _DirectAccessFileSystem : _MediaStoreFileSystem;

  private static bool IsInternalStorage(DirectoryDescription directoryDescription) =>
    directoryDescription.Root == System.Environment.SpecialFolder.ApplicationData;

  private sealed class MediaStoreFileSystem : IFileSystem
  {
    const string AndroidPathSeparator = "/";

    public string ToPath(DirectoryDescription directoryDescription) =>
      throw new ArgumentException($"{nameof(directoryDescription)} should be internal");

    public string ToPath(FileDescription fileDescription) =>
      throw new ArgumentException($"{nameof(fileDescription)} should be internal");

    public void RemoveFile(FileDescription fileDescription)
    {
      var uri = TryGetFileUri(fileDescription);
      if (uri is not null)
      {
        AndroidApplication.Context.ContentResolver?.Delete(uri, null, null);
      }
    }

    public void RemoveDirectory(DirectoryDescription directoryDescription) =>
      throw new NotImplementedException();

    public Stream OpenWriteStream(FileDescription fileDescription)
    {
#pragma warning disable CA1416
      Stream outStream;

      var uri = TryGetFileUri(fileDescription);
      if (uri is not null)
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

        var externalStorageUri = GetExternalStorageUri();
        uri = AndroidApplication.Context.ContentResolver?.Insert(externalStorageUri, values)
              ?? throw new IOException("Failed to create file via MediaStore.");
        outStream = AndroidApplication.Context.ContentResolver?.OpenOutputStream(uri, "wt")
                    ?? throw new IOException("Failed to open output stream to write.");
      }

      return outStream;
#pragma warning restore CA1416
    }

    public Stream OpenReadStream(FileDescription fileDescription)
    {
      var uri = TryGetFileUri(fileDescription);
      if (uri is null)
      {
        throw new IOException($"The file {fileDescription} doesn't exist");
      }

      var outStream = AndroidApplication.Context.ContentResolver?.OpenInputStream(uri)
                      ?? throw new IOException("Failed to open output stream to read.");
      return outStream;
    }

    public FileDescription[] GetFiles(DirectoryDescription directoryDescription, string searchPattern, bool recursive)
    {
      return GetFilesUri(directoryDescription, searchPattern, recursive).Keys.ToArray();
    }

    // Never reached: AndroidFileSystem composes copies from the per-argument stream operations.
    public void Copy(FileDescription from, FileDescription to) =>
      throw new NotSupportedException(nameof(Copy));

    public void Copy(Stream from, FileDescription to) =>
      throw new NotSupportedException(nameof(Copy));

    public bool FileExists(FileDescription fileDescription)
    {
      var uri = TryGetFileUri(fileDescription);
      return uri is not null;
    }

    public DateTime GetLastWriteTime(FileDescription fileDescription) =>
      throw new NotSupportedException(nameof(GetLastWriteTime));

    private static string GetAndroidRoot(DirectoryDescription directoryDescription)
    {
      return directoryDescription.Root switch
      {
        System.Environment.SpecialFolder.MyDocuments => Android.OS.Environment.DirectoryDocuments
          ?? throw new IOException($"{nameof(Android.OS.Environment.DirectoryDocuments)}"),

        System.Environment.SpecialFolder.MyPictures => Android.OS.Environment.DirectoryPictures
          ?? throw new IOException($"{nameof(Android.OS.Environment.DirectoryPictures)}"),

        System.Environment.SpecialFolder.MyMusic => Android.OS.Environment.DirectoryMusic
          ?? throw new IOException($"{nameof(Android.OS.Environment.DirectoryMusic)}"),

        _ => throw new NotSupportedException($"Not Supported root: {directoryDescription.Root}")
      };
    }

    private static string GetRelativePath(DirectoryDescription directoryDescription)
    {
      var chunks = new List<string>([GetAndroidRoot(directoryDescription)]);
      chunks.AddRange(directoryDescription.Path);
      var ret = Path.Combine(chunks.ToArray());

      return ret;
    }

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
#pragma warning disable CA1416
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

      AddLiveItemFilters(query);

      using var cursor = AndroidApplication.Context.ContentResolver?.Query(
        externalStorageUri,
        projection,
        string.Join(" AND ", query),
        args.ToArray(),
        sort);

      while (cursor?.MoveToNext() == true)
      {
        var documentId = cursor.GetLong(cursor.GetColumnIndexOrThrow(IBaseColumns.Id));
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
#pragma warning restore CA1416
    }

    private static void AddLiveItemFilters(List<string> query)
    {
#pragma warning disable CA1416
      query.Add($"{MediaStore.IMediaColumns.IsPending}=0");

      // Exclude items scheduled for purge from Trash
      // (DATE_EXPIRES is usually set for trashed items; use if you prefer)
      query.Add($"{MediaStore.IMediaColumns.DateExpires} IS NULL");

      if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // API 30+
      {
        // Exclude pending & trashed
        query.Add($"{MediaStore.IMediaColumns.IsTrashed}=0");
      }
#pragma warning restore CA1416
    }

    // For duplicate (RelativePath, DisplayName) rows the oldest live row wins, preserving the
    // historical GetFilesUri dictionary-overwrite behavior under the DateModified DESC sort.
    private static Android.Net.Uri? TryGetFileUri(FileDescription fileDescription)
    {
#pragma warning disable CA1416
      var externalStorageUri = GetExternalStorageUri();
      var query = new List<string>
      {
        $"{MediaStore.IMediaColumns.RelativePath}=?",
        $"{MediaStore.IMediaColumns.DisplayName}=?"
      };
      var args = new List<string>
      {
        EnsureTrailingSlash(GetRelativePath(fileDescription.Directory)),
        fileDescription.FileName
      };
      AddLiveItemFilters(query);

      using var cursor = AndroidApplication.Context.ContentResolver?.Query(
        externalStorageUri,
        [IBaseColumns.Id],
        string.Join(" AND ", query),
        args.ToArray(),
        $"{MediaStore.IMediaColumns.DateModified} DESC");

      Android.Net.Uri? ret = null;
      while (cursor?.MoveToNext() == true)
      {
        var documentId = cursor.GetLong(cursor.GetColumnIndexOrThrow(IBaseColumns.Id));
        var uri = ContentUris.WithAppendedId(externalStorageUri, documentId);
        if (ResourceExists(uri))
        {
          ret = uri;
        }
      }

      return ret;
#pragma warning restore CA1416
    }

    private static Android.Net.Uri GetExternalStorageUri()
    {
#pragma warning disable CA1416
      return MediaStore.Files.GetContentUri(MediaStore.VolumeExternalPrimary)
        ?? throw new IOException("Cannot get external URI");
#pragma warning restore CA1416
    }
  }
}
#endif
