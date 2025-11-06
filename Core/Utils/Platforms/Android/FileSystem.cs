using Android.Content;
using Android.Provider;

namespace GT4.Core.Utils;

internal class AndroidFileSystem : IFileSystem
{

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
    throw new NotFiniteNumberException();
  }

  public void RemoveDirectory(DirectoryDescription directoryDescription)
  {
    throw new NotFiniteNumberException();
  }

  public Stream OpenWriteStream(FileDescription fileDescription)
  {
    using var values = new ContentValues();
    values.Put(MediaStore.IMediaColumns.DisplayName, "myfile.txt");
    values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
    // Put the file under Documents/GT4
    values.Put(MediaStore.IMediaColumns.RelativePath, $"{Android.OS.Environment.DirectoryDocuments}/GT4");

    // Use the generic Files collection so we can target Documents
    var collection = MediaStore.Files.GetContentUri("external");
    //var uri = ctx.ContentResolver.Insert(collection!, values)
    //          ?? throw new IOException("Failed to create file via MediaStore.");

    //// Open a stream and write the content
    //using var outStream = ctx.ContentResolver.OpenOutputStream(uri, "w")
    //                      ?? throw new IOException("Failed to open output stream.");
    throw new NotFiniteNumberException();

  }

  public Stream OpenReadStream(FileDescription fileDescription)
  {
    throw new NotFiniteNumberException();
  }

  public FileDescription[] GetFiles(DirectoryDescription directoryDescription, string searchPattern, bool recursive)
  {
    throw new NotFiniteNumberException();
  }

  public void Copy(FileDescription from, FileDescription to)
  {
    using var sourceStream = OpenReadStream(from);
    using var targetStream = OpenWriteStream(to);
    sourceStream.CopyTo(targetStream);
    sourceStream.Flush();
  }
}
