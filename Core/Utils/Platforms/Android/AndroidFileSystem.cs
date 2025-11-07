using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidApplication = Android.App.Application;

namespace GT4.Core.Utils;

public class AndroidFileSystem : IFileSystem
{
  private readonly FileSystem _DirectAccessFileSystem = new();

  private string GetAndroidRoot(DirectoryDescription directoryDescription)
  {
    return directoryDescription.Root switch
    {
      System.Environment.SpecialFolder.MyDocuments => Android.OS.Environment.DirectoryDocuments
        ?? throw new IOException($"{nameof(Android.OS.Environment.DirectoryDocuments)}"),

      _ => throw new NotSupportedException($"Not Supported root: {directoryDescription.Root}")
    };
  }

  private string GetRelativePath(DirectoryDescription directoryDescription)
  {
    var chanks = new List<string>([GetAndroidRoot(directoryDescription)]);
    chanks.AddRange(directoryDescription.Path);
    var ret = Path.Combine(chanks.ToArray());

    return ret;
  }

  private Dictionary<FileDescription, Android.Net.Uri> GetFilesUri(DirectoryDescription directoryDescription, string searchPattern, bool recursive)
  {
    var ret = new Dictionary<FileDescription, Android.Net.Uri>();
    var externalStorageUri = GetExternalStorageUri();
    var query = $"{MediaStore.IMediaColumns.RelativePath}=?";
    var args = $"{GetRelativePath(directoryDescription)}/";
    var sort = $"{MediaStore.IMediaColumns.DateModified} DESC";
    var projection = new[]
    {
        Android.Provider.IBaseColumns.Id,
        MediaStore.IMediaColumns.DisplayName,
        MediaStore.IMediaColumns.MimeType,
        MediaStore.IMediaColumns.RelativePath,
        MediaStore.IMediaColumns.DateModified
    };

    using var cursor = AndroidApplication.Context.ContentResolver?.Query(externalStorageUri, projection, query, [args], sort);
    if (cursor is null)
    {
      return new();
    }

    while (cursor.MoveToNext())
    {
      var documentId = cursor.GetLong(cursor.GetColumnIndexOrThrow(Android.Provider.IBaseColumns.Id));
      var displayName = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.DisplayName));
      var mimeType = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.MimeType));
      var relativePath = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.RelativePath));
      var uri = ContentUris.WithAppendedId(externalStorageUri, documentId);
      IEnumerable<string> chunks = relativePath.Split("/");
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
    throw new NotFiniteNumberException();
  }

  public string ToPath(FileDescription fileDescription)
  {
    throw new NotFiniteNumberException();
  }

  public void RemoveFile(FileDescription fileDescription)
  {
    if (fileDescription.Directory.Root == System.Environment.SpecialFolder.ApplicationData)
    {
      _DirectAccessFileSystem.RemoveFile(fileDescription);
    }

    throw new NotFiniteNumberException();
  }

  public void RemoveDirectory(DirectoryDescription directoryDescription)
  {
    if (directoryDescription.Root == System.Environment.SpecialFolder.ApplicationData)
    {
      _DirectAccessFileSystem.RemoveDirectory(directoryDescription);
    }

    throw new NotFiniteNumberException();
  }

  public Stream OpenWriteStream(FileDescription fileDescription)
  {
    if (fileDescription.Directory.Root == System.Environment.SpecialFolder.ApplicationData)
    {
      return _DirectAccessFileSystem.OpenWriteStream(fileDescription);
    }

    using var values = new ContentValues();
    values.Put(MediaStore.IMediaColumns.DisplayName, fileDescription.FileName);
    values.Put(MediaStore.IMediaColumns.MimeType, fileDescription.MimeType);
    values.Put(MediaStore.IMediaColumns.RelativePath, GetRelativePath(fileDescription.Directory));

    var externaStoragelUri = GetExternalStorageUri();
    var uri = AndroidApplication.Context.ContentResolver?.Insert(externaStoragelUri, values)
             ?? throw new IOException("Failed to create file via MediaStore.");
    var outStream = AndroidApplication.Context.ContentResolver?.OpenOutputStream(uri, "w")
                    ?? throw new IOException("Failed to open output stream to write.");

    return outStream;
  }

  public Stream OpenReadStream(FileDescription fileDescription)
  {
    if (fileDescription.Directory.Root == System.Environment.SpecialFolder.ApplicationData)
    {
      return _DirectAccessFileSystem.OpenWriteStream(fileDescription);
    }

    if (!GetFilesUri(fileDescription.Directory, "", false).TryGetValue(fileDescription, out var uri))
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

    var ret = new List<FileDescription>();
    var externalStorageUri = GetExternalStorageUri();
    var query = $"{MediaStore.IMediaColumns.RelativePath}=?";
    var args = $"{GetRelativePath(directoryDescription)}/";
    var sort = $"{MediaStore.IMediaColumns.DateModified} DESC";
    var projection = new[]
    {
        Android.Provider.IBaseColumns.Id,
        MediaStore.IMediaColumns.DisplayName,
        MediaStore.IMediaColumns.MimeType,
        MediaStore.IMediaColumns.RelativePath,
        MediaStore.IMediaColumns.DateModified
    };

    using var cursor = AndroidApplication.Context.ContentResolver?.Query(externalStorageUri, projection, query, [args], sort);
    if (cursor is null)
    {
      return [];
    }

    while (cursor.MoveToNext())
    {
      var documentId = cursor.GetLong(cursor.GetColumnIndexOrThrow(Android.Provider.IBaseColumns.Id));
      var displayName = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.DisplayName));
      var mimeType = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.MimeType));
      var relativePath = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.RelativePath));
      var uri = ContentUris.WithAppendedId(externalStorageUri, documentId);
      var fileDescription = new FileDescription(
        Directory: new DirectoryDescription(
          Root: directoryDescription.Root,
          Path: relativePath?.Split("/").Skip(1).ToArray() ?? []),
        FileName: displayName ?? "",
        MimeType: mimeType);

      ret.Add(fileDescription);
    }

    return ret.ToArray();
  }

  public void Copy(FileDescription from, FileDescription to)
  {
    using var sourceStream = OpenReadStream(from);
    using var targetStream = OpenWriteStream(to);
    sourceStream.CopyTo(targetStream);
    sourceStream.Flush();
  }
}
