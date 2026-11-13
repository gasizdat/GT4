using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

// Separate from ImageUtilsTests, which reads headers rather than images: these decode and re-encode
// real bitmaps, which is testable off a platform host only because the decode is SkiaSharp's.
public class ImageDownsizingTests
{
  [Fact]
  public void LongestSideIsScaledToMaxSize()
  {
    var original = PngOfSize(400, 200);

    var downsized = ImageUtils.DownsizedPng(original, 100);

    PixelSizeOf(downsized).Should().Be(new Size(100, 50));
  }

  [Fact]
  public void TallerThanItIsWideScalesOnHeight()
  {
    var original = PngOfSize(200, 400);

    var downsized = ImageUtils.DownsizedPng(original, 100);

    PixelSizeOf(downsized).Should().Be(new Size(50, 100));
  }

  // Nothing is gained by blowing a small image up to the thumbnail box -- the stub images shipped as
  // raw resources are already smaller than it, and upscaling them would only cost memory.
  [Fact]
  public void SmallerThanMaxSizeKeepsItsDimensions()
  {
    var original = PngOfSize(40, 30);

    var downsized = ImageUtils.DownsizedPng(original, 100);

    PixelSizeOf(downsized).Should().Be(new Size(40, 30));
  }

  [Fact]
  public void RotatedPhotoIsScaledUpright()
  {
    var original = UprightDecodeTests.MarkedJpeg(SKEncodedOrigin.RightTop);

    var downsized = ImageUtils.DownsizedPng(original, 32);

    PixelSizeOf(downsized).Should().Be(new Size(16, 32));
  }

  [Fact]
  public void UndecodableBytesComeBackAsNull()
  {
    byte[] notAnImage = [0x00, 0x01, 0x02, 0x03];

    var downsized = ImageUtils.DownsizedPng(notAnImage, 100);

    downsized.Should().BeNull();
  }

  private static byte[] PngOfSize(int width, int height)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Teal);
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static Size PixelSizeOf(byte[]? data)
  {
    data.Should().NotBeNull();
    var size = ImageUtils.PixelSize(data!);
    size.Should().NotBeNull();
    return size!.Value;
  }
}
