#if WINDOWS
using SkiaSharp;
using System.Runtime.InteropServices;

namespace GT4.UI.Utils.Platforms.Windows;

internal static class BitmapColors
{
  // A new SKBitmap is Bgra8888 on Windows, whose bytes read little-endian are SKColor's 0xAARRGGBB --
  // but premultiplied, so only opaque pixels read back as written. A photo can be translucent, or
  // Gray8 (a decoded grayscale JPEG): read it through SKBitmap.Pixels, which converts like GetPixel.
  public static Span<SKColor> Of(SKBitmap bitmap) => MemoryMarshal.Cast<byte, SKColor>(bitmap.GetPixelSpan());
}
#endif
