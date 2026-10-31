using SkiaSharp;

namespace GT4.UI.Abstraction;

public interface IPhotoEnhancement
{
  string DisplayName { get; }
  Task<SKBitmap> ApplyAsync(SKBitmap bitmap, CancellationToken token);
}
