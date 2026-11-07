#if WINDOWS
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class UpscalePhotoEnhancement : IPhotoEnhancement, IModelDefinition
{
  private static readonly ModelDescriptor Model = new(
    "realesr-general-x4v3.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v1/realesr-general-x4v3.onnx"),
    "924EBAD6532777303582D4CE7811849B88869231A3AE7093E0F21200249DF8D5",
    "Real-ESRGAN",
    "BSD-3-Clause",
    4866396);
  private const int ModelScale = 4;
  // The model's result is halved, as Real-ESRGAN's own outscale option does: a quarter of the pixels
  // to store and decode, still twice the source's size.
  private const int OutputScale = 2;
  private const int MaxPixels = 1024 * 1024;
  private const int TileSize = 512;
  // The model stacks 34 3x3 convolutions, so an output pixel depends on input pixels up to 34 away;
  // a tile padded by that much comes out identical to a single pass over the whole photo.
  private const int TilePadding = 34;

  private readonly IModelSessionProvider _SessionProvider;

  public UpscalePhotoEnhancement(IModelSessionProvider sessionProvider)
  {
    _SessionProvider = sessionProvider;
  }

  public string DisplayName => UIStrings.EnhancementUpscale;

  public string CaptionMark => string.Format(UIStrings.PhotoMarkUpscale_1, OutputScale);

  public ModelDescriptor[] Models => [Model];

  public async Task<SKBitmap> ApplyAsync(SKBitmap bitmap, CancellationToken token)
  {
    const double Megapixel = 1_000_000;

    token.ThrowIfCancellationRequested();

    var pixels = bitmap.Width * bitmap.Height;
    if (pixels > MaxPixels)
    {
      var message = string.Format(UIStrings.ErrorPhotoTooLarge_3, DisplayName, MaxPixels / Megapixel, pixels / Megapixel);
      throw new InvalidOperationException(message);
    }

    var session = await _SessionProvider.GetSessionAsync(Model, token);

    var inputName = session.InputMetadata.Keys.First();
    var outputName = session.OutputMetadata.Keys.First();
    using var source = bitmap.Copy(SKColorType.Bgra8888);
    using var upscaled = new SKBitmap(bitmap.Width * ModelScale, bitmap.Height * ModelScale);
    for (var tileY = 0; tileY < bitmap.Height; tileY += TileSize)
    {
      for (var tileX = 0; tileX < bitmap.Width; tileX += TileSize)
      {
        token.ThrowIfCancellationRequested();
        var tile = SKRectI.Create(tileX, tileY, TileSize, TileSize);
        tile.Intersect(bitmap.Info.Rect);
        var padded = SKRectI.Inflate(tile, TilePadding, TilePadding);
        padded.Intersect(bitmap.Info.Rect);
        var input = ToInputTensor(source, padded);

        using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
        var output = (DenseTensor<float>)results.First(r => r.Name == outputName).AsTensor<float>();
        CopyTileInto(upscaled, output, tile, padded);
      }
    }

    var info = new SKImageInfo(bitmap.Width * OutputScale, bitmap.Height * OutputScale);
    var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
    return upscaled.Resize(info, sampling);
  }

  public void ReleaseModels() => _SessionProvider.Release(Models);

  private static DenseTensor<float> ToInputTensor(SKBitmap bitmap, SKRectI area)
  {
    var tensor = new DenseTensor<float>([1, 3, area.Height, area.Width]);
    var values = tensor.Buffer.Span;
    var plane = area.Width * area.Height;
    var pixels = BitmapColors.Of(bitmap);
    for (var y = 0; y < area.Height; y++)
    {
      for (var x = 0; x < area.Width; x++)
      {
        var pixel = pixels[(area.Top + y) * bitmap.Width + area.Left + x];
        var i = y * area.Width + x;
        values[i] = pixel.Red / 255f;
        values[plane + i] = pixel.Green / 255f;
        values[2 * plane + i] = pixel.Blue / 255f;
      }
    }
    return tensor;
  }

  private static void CopyTileInto(SKBitmap destination, DenseTensor<float> output, SKRectI tile, SKRectI padded)
  {
    static byte Clamp(float v) => (byte)Math.Clamp(v * 255f, 0f, 255f);

    var values = output.Buffer.Span;
    var outputWidth = output.Dimensions[3];
    var plane = output.Dimensions[2] * outputWidth;
    var pixels = BitmapColors.Of(destination);
    for (var y = tile.Top * ModelScale; y < tile.Bottom * ModelScale; y++)
    {
      var outputY = y - padded.Top * ModelScale;
      for (var x = tile.Left * ModelScale; x < tile.Right * ModelScale; x++)
      {
        var outputX = x - padded.Left * ModelScale;
        var i = outputY * outputWidth + outputX;
        var red = Clamp(values[i]);
        var green = Clamp(values[plane + i]);
        var blue = Clamp(values[2 * plane + i]);
        var color = new SKColor(red, green, blue);
        pixels[y * destination.Width + x] = color;
      }
    }
  }
}
#endif
