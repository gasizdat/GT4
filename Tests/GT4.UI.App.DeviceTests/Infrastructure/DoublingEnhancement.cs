using GT4.UI.Abstraction;
using SkiaSharp;

namespace GT4.UI.DeviceTests;

internal sealed class DoublingEnhancement : IPhotoEnhancement
{
  public SKSizeI? Received { get; private set; }

  public string DisplayName { get; init; } = "Double";

  public ModelDescriptor[] Models { get; init; } = [];

  public Task<SKBitmap> ApplyAsync(SKBitmap bitmap, CancellationToken token)
  {
    Received = new SKSizeI(bitmap.Width, bitmap.Height);
    var doubled = new SKBitmap(bitmap.Width * 2, bitmap.Height * 2);
    doubled.Erase(SKColors.Blue);
    return Task.FromResult(doubled);
  }
}
