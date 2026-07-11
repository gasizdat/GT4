using GT4.Core.Project.Dto;
using Microsoft.Maui.Graphics.Platform;

namespace GT4.UI.Utils;

public static class ImageUtils
{
  public static string DefaultPhotoResourceName(BiologicalSex biologicalSex) => biologicalSex switch
  {
    BiologicalSex.Male => "male_stub.png",
    BiologicalSex.Female => "female_stub.png",
    _ => "project_icon.png",
  };

  private static readonly byte[] TransparentPng =
  {
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x05, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0x64, 0xF8, 0x0F, 0x00,
    0x01, 0x05, 0x01, 0x27, 0x23, 0xE3, 0x66, 0x66, 0x00, 0x00, 0x00, 0x00,
    0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
  };

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
    ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data.Length > 0 ? data : TransparentPng), token));

  /// <summary>
  /// Decodes <paramref name="data"/>, scales it so its longest side is <paramref name="maxSize"/> and
  /// re-encodes it as PNG. Lets callers keep a small thumbnail instead of a full-resolution bitmap when
  /// the image is only ever shown tiny (a family-tree node decodes its source to a ~60px circle, so the
  /// full-res decode is hundreds of times larger than needed).
  /// </summary>
  public static byte[] DownsizedPng(byte[] data, float maxSize)
  {
    using var input = new MemoryStream(data);
    using var image = PlatformImage.FromStream(input);
    using var resized = image.Downsize(maxSize);
    using var output = new MemoryStream();
    resized.Save(output);

    return output.ToArray();
  }

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
