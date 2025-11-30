namespace GT4.UI;

public static class ImageUtils
{
  private static async Task<byte[]?> ToBytesAsync(Stream? stream, CancellationToken token)
  {
    if (stream is null)
    {
      return null;
    }

    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms, token).ConfigureAwait(false);

    return ms.ToArray();
  }

  public static ImageSource ImageFromBytes(byte[] data) =>
    ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data), token));

  public static ImageSource ImageFromRawResource(string resourceName) =>
    ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync(resourceName));

  public static async Task<byte[]?> ToBytesAsync(ImageSource? source, CancellationToken token)
  {
    if (source == null)
    {
      return null;
    }

    var httpStreamReaderAsync = async (Uri uri, CancellationToken token) =>
    {
      // TODO switch to HTTP Client provider
      using var http = new HttpClient();
      return await http.GetStreamAsync(uri, token).ConfigureAwait(false);
    };

    Stream? stream = source switch
    {
      FileImageSource fileImageSource => File.OpenRead(fileImageSource.File),
      StreamImageSource streamImageSource => await streamImageSource
        .Stream(token)
        .ConfigureAwait(false),
      UriImageSource uriImageSource => await httpStreamReaderAsync(uriImageSource.Uri, token),
      _ => throw new NotSupportedException("Unsupported stream type"),
    };

    if (stream == null)
    {
      throw new InvalidOperationException($"Could not obtain a stream from {source.GetType().Name}.");
    }
    using (stream)
    {
      return await ToBytesAsync(stream, token);
    }
  }

  public static async Task<byte[]?> ToBytesAsync(string resourceName, CancellationToken token)
  {
    using var stream = await FileSystem.OpenAppPackageFileAsync(resourceName);
    return await ToBytesAsync(stream, token);
  }

}
