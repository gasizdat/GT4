using SkiaSharp;

namespace GT4.UI.Abstraction;

public interface IPhotoEnhancement : IModelDefinition
{
  string DisplayName { get; }
  string CaptionMark { get; }
  string MenuLabel { get; }
  Task<SKBitmap> ApplyAsync(SKBitmap bitmap, CancellationToken token);
}
