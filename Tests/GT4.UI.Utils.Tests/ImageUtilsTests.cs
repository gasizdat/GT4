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

  [Theory]
  [InlineData(true, 1, 4000, 3000)]
  [InlineData(true, 3, 4000, 3000)]
  [InlineData(true, 6, 3000, 4000)]
  [InlineData(true, 8, 3000, 4000)]
  [InlineData(false, 3, 4000, 3000)]
  [InlineData(false, 6, 3000, 4000)]
  public void Jpeg_SwapsItsDimensionsOnlyForAQuarterTurnOrientation(bool littleEndian, ushort orientation, int width, int height)
  {
    var exif = ExifSegment(orientation, littleEndian);
    var jpeg = JpegOf(4000, 3000, exif);

    PixelSizeOf(jpeg).Should().Be(new Size(width, height));
  }

  [Fact]
  public void Jpeg_KeepsItsExifQuarterTurnPastALaterXmpSegment()
  {
    var exif = ExifSegment(6, littleEndian: true);
    byte[] xmpPayload = [.. "http://ns.adobe.com/xap/1.0/\0"u8, .. "<x:xmpmeta/>"u8];
    var xmp = Segment(0xE1, xmpPayload);
    var jpeg = JpegOf(4000, 3000, exif, xmp);

    PixelSizeOf(jpeg).Should().Be(new Size(3000, 4000));
  }

  [Fact]
  public void Jpeg_WithAnIfdOffsetPastItsExifSegment_KeepsItsStoredSize()
  {
    var exif = ExifSegment(6, littleEndian: true);
    // The IFD0 offset's low byte, right after the APP1 header, the Exif identifier and "II*\0".
    exif[4 + 6 + 4] = 0xFF;
    var jpeg = JpegOf(4000, 3000, exif);

    PixelSizeOf(jpeg).Should().Be(new Size(4000, 3000));
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

  private static byte[] JpegOf(ushort width, ushort height, params byte[][] segments)
  {
    var frame = new byte[13];
    frame[0] = 0xFF;
    frame[1] = 0xC0;
    WriteBigEndian16(frame.AsSpan(2), 0x11);
    frame[4] = 0x08;
    WriteBigEndian16(frame.AsSpan(5), height);
    WriteBigEndian16(frame.AsSpan(7), width);
    return [0xFF, 0xD8, .. segments.SelectMany(segment => segment), .. frame];
  }

  // A TIFF header and an IFD0 holding only the orientation tag, a single SHORT.
  private static byte[] ExifSegment(ushort orientation, bool littleEndian)
  {
    var tiff = new byte[26];
    var byteOrder = littleEndian ? "II"u8 : "MM"u8;
    byteOrder.CopyTo(tiff);
    WriteTiff16(tiff.AsSpan(2), 42, littleEndian);
    WriteTiff32(tiff.AsSpan(4), 8, littleEndian);
    WriteTiff16(tiff.AsSpan(8), 1, littleEndian);
    WriteTiff16(tiff.AsSpan(10), 0x0112, littleEndian);
    WriteTiff16(tiff.AsSpan(12), 3, littleEndian);
    WriteTiff32(tiff.AsSpan(14), 1, littleEndian);
    WriteTiff16(tiff.AsSpan(18), orientation, littleEndian);
    return Segment(0xE1, [.. "Exif\0\0"u8, .. tiff]);
  }

  private static byte[] Segment(byte marker, byte[] payload)
  {
    var segment = new byte[4 + payload.Length];
    segment[0] = 0xFF;
    segment[1] = marker;
    WriteBigEndian16(segment.AsSpan(2), (ushort)(2 + payload.Length));
    payload.CopyTo(segment, 4);
    return segment;
  }

  private static void WriteTiff16(Span<byte> target, ushort value, bool littleEndian)
  {
    if (littleEndian)
    {
      BinaryPrimitives.WriteUInt16LittleEndian(target, value);
    }
    else
    {
      BinaryPrimitives.WriteUInt16BigEndian(target, value);
    }
  }

  private static void WriteTiff32(Span<byte> target, uint value, bool littleEndian)
  {
    if (littleEndian)
    {
      BinaryPrimitives.WriteUInt32LittleEndian(target, value);
    }
    else
    {
      BinaryPrimitives.WriteUInt32BigEndian(target, value);
    }
  }

  private static void WriteBigEndian32(Span<byte> target, uint value) =>
    BinaryPrimitives.WriteUInt32BigEndian(target, value);

  private static void WriteBigEndian16(Span<byte> target, ushort value) =>
    BinaryPrimitives.WriteUInt16BigEndian(target, value);
}
