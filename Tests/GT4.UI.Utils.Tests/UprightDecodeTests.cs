using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class UprightDecodeTests
{
  private const int StoredWidth = 64;
  private const int StoredHeight = 32;

  // The stored top-left corner is marked, and the image isn't square, so the corner it lands in plus the
  // decoded dimensions tell all eight orientations apart -- rotating the wrong way or mirroring included.
  [Theory]
  [InlineData(SKEncodedOrigin.TopLeft, 64, 32, false, false)]
  [InlineData(SKEncodedOrigin.TopRight, 64, 32, true, false)]
  [InlineData(SKEncodedOrigin.BottomRight, 64, 32, true, true)]
  [InlineData(SKEncodedOrigin.BottomLeft, 64, 32, false, true)]
  [InlineData(SKEncodedOrigin.LeftTop, 32, 64, false, false)]
  [InlineData(SKEncodedOrigin.RightTop, 32, 64, true, false)]
  [InlineData(SKEncodedOrigin.RightBottom, 32, 64, true, true)]
  [InlineData(SKEncodedOrigin.LeftBottom, 32, 64, false, true)]
  public void StoredTopLeftCornerLandsWhereTheOrientationTurnsIt(
    SKEncodedOrigin origin, int width, int height, bool markedRight, bool markedBottom)
  {
    using var stream = new MemoryStream(MarkedJpeg(origin));

    using var bitmap = ImageUtils.DecodeUpright(stream);

    bitmap.Should().NotBeNull();
    bitmap!.Width.Should().Be(width);
    bitmap.Height.Should().Be(height);
    foreach (var right in new[] { false, true })
    {
      foreach (var bottom in new[] { false, true })
      {
        var x = right ? width - 9 : 8;
        var y = bottom ? height - 9 : 8;
        var marked = right == markedRight && bottom == markedBottom;
        IsRed(bitmap.GetPixel(x, y)).Should().Be(marked, $"the {(bottom ? "bottom" : "top")}-{(right ? "right" : "left")} corner");
      }
    }
  }

  [Fact]
  public void UntaggedImageDecodesAsStored()
  {
    using var stream = new MemoryStream(MarkedJpegWithoutExif());

    using var bitmap = ImageUtils.DecodeUpright(stream);

    bitmap.Should().NotBeNull();
    bitmap!.Width.Should().Be(StoredWidth);
    bitmap.Height.Should().Be(StoredHeight);
    IsRed(bitmap.GetPixel(8, 8)).Should().BeTrue();
  }

  [Fact]
  public void UndecodableBytesGiveNull()
  {
    using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03]);

    using var bitmap = ImageUtils.DecodeUpright(stream);

    bitmap.Should().BeNull();
  }

  private static bool IsRed(SKColor color) => color.Red > 200 && color.Green < 60 && color.Blue < 60;

  // A 32x16 red block, aligned to the JPEG's 16px blocks so compression can't smear it into the samples.
  private static byte[] MarkedJpegWithoutExif()
  {
    using var bitmap = new SKBitmap(StoredWidth, StoredHeight);
    using (var canvas = new SKCanvas(bitmap))
    {
      canvas.Clear(SKColors.Black);
      using var paint = new SKPaint { Color = SKColors.Red };
      canvas.DrawRect(0, 0, 32, 16, paint);
    }
    using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
    return data.ToArray();
  }

  // Skia's encoder writes no EXIF, so an APP1 segment carrying only the orientation tag is spliced in
  // after the SOI marker.
  internal static byte[] MarkedJpeg(SKEncodedOrigin origin)
  {
    var app1 = ImageUtilsTests.ExifSegment((ushort)origin, littleEndian: true);
    var jpeg = MarkedJpegWithoutExif();
    byte[] tagged = [.. jpeg[..2], .. app1, .. jpeg[2..]];

    using var codec = SKCodec.Create(new MemoryStream(tagged));
    codec.EncodedOrigin.Should().Be(origin, "the spliced EXIF segment must be readable, or every case decodes as TopLeft");
    return tagged;
  }
}
