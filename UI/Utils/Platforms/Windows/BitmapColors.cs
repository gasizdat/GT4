#if WINDOWS
using SkiaSharp;
using System.Runtime.InteropServices;

namespace GT4.UI.Utils.Platforms.Windows;

internal static class BitmapColors
{
  // Bgra8888 only: its bytes, read little-endian, are SKColor's 0xAARRGGBB. A new SKBitmap is
  // Bgra8888 on Windows; a decoded grayscale JPEG is Gray8.
  public static Span<SKColor> Of(SKBitmap bitmap) => MemoryMarshal.Cast<byte, SKColor>(bitmap.GetPixelSpan());
}
#endif
