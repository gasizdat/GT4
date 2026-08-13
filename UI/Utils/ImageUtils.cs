using GT4.Core.Project.Dto;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Maui.Graphics.Platform;
using System.Buffers.Binary;

namespace GT4.UI.Utils;

public static class ImageUtils
{
  private static readonly MemoryCache _MemoryCache = new MemoryCache(new MemoryCacheOptions());
  private static readonly byte[] _PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
  private static readonly byte[] _JpegSignature = [0xFF, 0xD8];
  private static readonly byte[] _GifSignature = [.. "GIF8"u8];
  private static readonly byte[] _BmpSignature = [.. "BM"u8];

  internal static readonly byte[] TransparentPngData =
  {
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x05, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0x64, 0xF8, 0x0F, 0x00,
    0x01, 0x05, 0x01, 0x27, 0x23, 0xE3, 0x66, 0x66, 0x00, 0x00, 0x00, 0x00,
    0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
  };

  public static string DefaultPersonPhotoResourceName(BiologicalSex biologicalSex) => biologicalSex switch
  {
    BiologicalSex.Male => "male_stub.png",
    BiologicalSex.Female => "female_stub.png",
    _ => "project_icon.png",
  };

  public const int ThumbnailSize = 100;

  public static ImageSource TransparentImageStub =>
    ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(TransparentPngData), token));

  // Keyed on Data.Id, not the more general ElementId: Data.Id is a TableData AUTOINCREMENT row id, the
  // only id space in this app guaranteed unique against every other cached entry. PersonInfo.Id etc. come
  // from unrelated tables and can collide with it numerically, which would hand back another photo's bytes.
  public static ImageSource ImageFromBytes(Data data, byte[] content, int? maxSize)
  {
    if (content.Length == 0)
    {
      return TransparentImageStub;
    }

    if (data.Id == ElementId.NonCommittedId)
    {
      return ImageSource.FromStream(token => Task.Run<Stream>(
        () => new MemoryStream(maxSize.HasValue ? DownsizedPng(content, maxSize.Value) : content), token));
    }

    var cacheKeySuffix = maxSize.HasValue ? $"{data.Id}_{maxSize}" : $"{data.Id}";

    return _MemoryCache.GetOrCreate($"custom_image_{cacheKeySuffix}", entry =>
    {
      entry.Size = 1;
      return ImageSource.FromStream(async _ =>
      {
        var cachedData = _MemoryCache.GetOrCreate($"custom_data_{cacheKeySuffix}", async dataEntry =>
        {
          var bytes = maxSize.HasValue ? DownsizedPng(content, maxSize.Value) : content;

          dataEntry.Size = bytes.Length;
          return bytes;
        });

        return new MemoryStream(cachedData is null ? TransparentPngData : await cachedData);
      });
    })!;
  }

  /// <summary>
  /// Decodes <paramref name="data"/>, scales it so its longest side is <paramref name="maxSize"/> and
  /// re-encodes it as PNG. Lets callers keep a small thumbnail instead of a full-resolution bitmap when
  /// the image is only ever shown tiny (a family-tree node decodes its source to a ~60px circle, so the
  /// full-res decode is hundreds of times larger than needed).
  /// </summary>
  public static byte[] DownsizedPng(byte[] data, int maxSize)
  {
    using var input = new MemoryStream(data);
    return DownsizedPngStream(input, maxSize);
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
    else if (bytes.StartsWith(_JpegSignature))
    {
      size = JpegPixelSize(bytes);
    }
    else if (bytes.StartsWith(_GifSignature))
    {
      size = GifPixelSize(bytes);
    }
    else if (bytes.StartsWith(_BmpSignature))
    {
      size = BmpPixelSize(bytes);
    }

    return size is { Width: > 0, Height: > 0 } ? size : null;
  }

  public static ImageSource ImageFromRawResource(string resourceName, int? maxSize)
  {
    var cacheKeySuffix = maxSize.HasValue ? $"{resourceName}_{maxSize}" : resourceName;

    return _MemoryCache.GetOrCreate($"image_{cacheKeySuffix}", entry =>
    {
      entry.Size = 1;
      return ImageSource.FromStream(async _ =>
      {
        var cachedData = _MemoryCache.GetOrCreate($"data_{cacheKeySuffix}", async dataEntry =>
        {
          byte[] bytes;
          if (maxSize.HasValue)
          {
            using var originalStream = await FileSystem.OpenAppPackageFileAsync(resourceName);
            bytes = DownsizedPngStream(originalStream, maxSize.Value);
          }
          else
          {
            using var tempStream = new MemoryStream();
            using (var originalStream = await FileSystem.OpenAppPackageFileAsync(resourceName))
            {
              originalStream.CopyTo(tempStream);
            }
            bytes = tempStream.ToArray();
          }

          dataEntry.Size = bytes.Length;
          return bytes;
        });

        return new MemoryStream(cachedData is null ? [] : await cachedData);
      });
    })!;
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

  private static byte[] DownsizedPngStream(Stream input, float maxSize)
  {
    using var image = PlatformImage.FromStream(input);
    using var resized = image.Downsize(maxSize, disposeOriginal: true);
    using var output = new MemoryStream();
    resized.Save(output);

    return output.ToArray();
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
