namespace GT4.UI;

public static class ImageUtils
{
  public static ImageSource ImageFromBytes(byte[] data) =>
    ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data), token));

  public static ImageSource ImageFromRawResource(string resourceName) =>
    ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync(resourceName));
}