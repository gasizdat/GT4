using FluentAssertions;
using System.Buffers.Binary;
using Xunit;

namespace GT4.UI.Utils.Tests;

// PixelSize reads headers rather than decoding, so these are the real formats' byte layouts rather
// than images: each case is the shortest header that carries the two numbers.
public class ImageUtilsTests
{
  private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  [Fact]
  public void Png_ReadsItsIhdrDimensions()
  {
    var png = new byte[24];
    PngSignature.CopyTo(png, 0);
    "IHDR"u8.CopyTo(png.AsSpan(12));
    WriteBigEndian32(png.AsSpan(16), 1920);
    WriteBigEndian32(png.AsSpan(20), 1080);

    PixelSizeOf(png).Should().Be(new Size(1920, 1080));
  }

  [Fact]
  public void Jpeg_ReadsTheDimensionsOfItsStartOfFrame()
  {
    // SOI, then an APP0 segment to hop over, then SOF0 carrying 4000x3000.
    var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08 };
    var frame = new byte[jpeg.Length + 8];
    jpeg.CopyTo(frame, 0);
    WriteBigEndian16(frame.AsSpan(jpeg.Length), 3000);
    WriteBigEndian16(frame.AsSpan(jpeg.Length + 2), 4000);

    PixelSizeOf(frame).Should().Be(new Size(4000, 3000));
  }

  [Fact]
  public void Jpeg_SkipsAHuffmanTableSharingTheStartOfFrameRange()
  {
    // 0xC4 sits inside 0xC0-0xCF but is a Huffman table; reading it as a frame would give 0x0800 x 0x0011.
    var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xC4, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08 };
    var frame = new byte[jpeg.Length + 8];
    jpeg.CopyTo(frame, 0);
    WriteBigEndian16(frame.AsSpan(jpeg.Length), 480);
    WriteBigEndian16(frame.AsSpan(jpeg.Length + 2), 640);

    PixelSizeOf(frame).Should().Be(new Size(640, 480));
  }

  [Fact]
  public void Gif_ReadsItsLittleEndianScreenSize()
  {
    var gif = new byte[10];
    "GIF89a"u8.CopyTo(gif);
    BitConverter.GetBytes((ushort)320).CopyTo(gif, 6);
    BitConverter.GetBytes((ushort)240).CopyTo(gif, 8);

    PixelSizeOf(gif).Should().Be(new Size(320, 240));
  }

  [Fact]
  public void Bmp_TreatsANegativeHeightAsTopDownRowOrder()
  {
    var bmp = new byte[26];
    "BM"u8.CopyTo(bmp);
    BitConverter.GetBytes(800).CopyTo(bmp, 18);
    BitConverter.GetBytes(-600).CopyTo(bmp, 22);

    PixelSizeOf(bmp).Should().Be(new Size(800, 600));
  }

  [Theory]
  [InlineData(new byte[0])]
  [InlineData(new byte[] { 0x00 })]
  [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
  [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
  [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 })]
  public void UnreadableBytes_HaveNoSize(byte[] data)
  {
    ImageUtils.PixelSize(data).Should().BeNull();
  }

  [Fact]
  public void ZeroDimensions_AreNotASize()
  {
    var png = new byte[24];
    PngSignature.CopyTo(png, 0);
    "IHDR"u8.CopyTo(png.AsSpan(12));

    ImageUtils.PixelSize(png).Should().BeNull();
  }

  private static Size PixelSizeOf(byte[] data)
  {
    var size = ImageUtils.PixelSize(data);
    size.Should().NotBeNull();
    return size!.Value;
  }

  private static void WriteBigEndian32(Span<byte> target, uint value) =>
    BinaryPrimitives.WriteUInt32BigEndian(target, value);

  private static void WriteBigEndian16(Span<byte> target, ushort value) =>
    BinaryPrimitives.WriteUInt16BigEndian(target, value);
}
