using GT4.Core.Project.Dto;
using GT4.UI.Utils.Converters;
using Microsoft.Maui.Graphics.Platform;
using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace GT4.UI.Utils;

public static class ImageUtils
{
  private static readonly byte[] _PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static readonly byte[] _TransparentPng =
  {
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x05, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0x64, 0xF8, 0x0F, 0x00,
    0x01, 0x05, 0x01, 0x27, 0x23, 0xE3, 0x66, 0x66, 0x00, 0x00, 0x00, 0x00,
    0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
  };

  // ImageSource.FromStream results have no stable identity MAUI's platform image cache can key on,
  // so each call independently decodes -- caching by resource name lets repeated requests for the
  // same default stub image (male/female/project icon) reuse one decode. These are static app
  // assets that never change, so entries never need eviction.
  private static readonly ConcurrentDictionary<string, ImageSource> _ImageCache = new();

  public static string DefaultPersonPhotoResourceName(BiologicalSex biologicalSex) => biologicalSex switch
  {
    BiologicalSex.Male => "male_stub.png",
    BiologicalSex.Female => "female_stub.png",
    _ => "project_icon.png",
  };

  public static ImageSource ImageFromBytes(byte[] data) =>
    ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data.Length > 0 ? data : _TransparentPng), token));

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

  /// <summary>
  /// The image's own pixel dimensions read straight from its header, or null for anything malformed or
  /// encoded some other way -- callers lay such an image out without an aspect ratio rather than failing
  /// the render around it. Read rather than decoded on purpose: <c>PlatformImage.FromStream</c> yields
  /// the same two numbers but costs ~110ms for a 12MP photo, on the UI thread, per image in a biography.
  /// </summary>
  public static Size? PixelSize(byte[] data)
  {
    var bytes = data.AsSpan();
    Size? size = null;

    if (bytes.StartsWith(_PngSignature))
    {
      size = PngPixelSize(bytes);
    }
    else if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
    {
      size = JpegPixelSize(bytes);
    }
    else if (bytes.StartsWith("GIF8"u8))
    {
      size = GifPixelSize(bytes);
    }
    else if (bytes.StartsWith("BM"u8))
    {
      size = BmpPixelSize(bytes);
    }

    return size is { Width: > 0, Height: > 0 } ? size : null;
  }

  public static ImageSource ImageFromRawResource(string resourceName)
  {
    if (!_ImageCache.TryGetValue(resourceName, out var image))
    {
      image = ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync(resourceName));
      _ImageCache.TryAdd(resourceName, image);
    }

    return image;
  }
  /// <summary>
  /// Resolves a photo through its category's keyed <see cref="IDataConverter"/>, falling back to a
  /// caption-less <paramref name="fallback"/> both when no converter is registered for the category (an
  /// older build opening a project that has a category it doesn't know about) and when a registered
  /// converter hands back something that isn't a <see cref="PhotoInfo"/>.
  /// </summary>
  public static async Task<PhotoInfo> ResolvePhotoAsync(
    OptionalDataConverterResolver dataConverterResolver, Data data, ImageSource fallback, CancellationToken token)
  {
    var converter = dataConverterResolver(data.Category);
    var resolved = converter is null ? null : await converter.ToObjectAsync(data, token);

    if (resolved is PhotoInfo photoInfo)
    {
      return photoInfo;
    }

    System.Diagnostics.Debug.WriteLine($"No usable IDataConverter result for {data.Category}; using fallback image.");
    return new PhotoInfo(fallback, null);
  }

  public static async Task<byte[]?> ToBytesAsync(ImageSource? source, IHttpClientFactory httpClientFactory, CancellationToken token)
  {
    if (source == null)
    {
      return null;
    }

    var httpStreamReaderAsync = async (Uri uri, CancellationToken token) =>
    {
      using var http = httpClientFactory.CreateClient();
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

  private static Size? PngPixelSize(ReadOnlySpan<byte> bytes)
  {
    if (bytes.Length < 24 || !bytes[12..16].SequenceEqual("IHDR"u8))
    {
      return null;
    }

    var width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);
    var height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
    return new Size(width, height);
  }

  // Segment-hop from the SOI to the start-of-frame that carries the dimensions. Anything unexpected on
  // the way -- a missing marker, a length that wouldn't advance -- gives up rather than guessing.
  private static Size? JpegPixelSize(ReadOnlySpan<byte> bytes)
  {
    for (var offset = 2; offset + 9 < bytes.Length;)
    {
      if (bytes[offset] != 0xFF)
      {
        return null;
      }

      var marker = bytes[offset + 1];
      if (IsStartOfFrame(marker))
      {
        var height = BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]);
        var width = BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 7)..]);
        return new Size(width, height);
      }

      var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 2)..]);
      if (length < 2)
      {
        return null;
      }

      offset += 2 + length;
    }

    return null;
  }

  // SOF0-SOF15 all carry the frame header; C4, C8 and CC sit in the same range but are the Huffman and
  // arithmetic-coding tables instead.
  private static bool IsStartOfFrame(byte marker) =>
    marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

  private static Size? GifPixelSize(ReadOnlySpan<byte> bytes) =>
    bytes.Length < 10
      ? null
      : new Size(BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]));

  // A negative height is the top-down row order, not a size.
  private static Size? BmpPixelSize(ReadOnlySpan<byte> bytes)
  {
    if (bytes.Length < 26)
    {
      return null;
    }

    var width = BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]);
    var height = BinaryPrimitives.ReadInt32LittleEndian(bytes[22..]);
    return new Size(width, Math.Abs(height));
  }

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

}
