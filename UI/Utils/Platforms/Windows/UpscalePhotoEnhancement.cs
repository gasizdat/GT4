#if WINDOWS
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class UpscalePhotoEnhancement : IPhotoEnhancement, IModelDefinition, IDisposable
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

  private readonly IModelSessionFactory _SessionFactory;
  private InferenceSession? _Session;

  public UpscalePhotoEnhancement(IModelSessionFactory sessionFactory)
  {
    _SessionFactory = sessionFactory;
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

    var session = _Session ??= await _SessionFactory.LoadSessionAsync(Model, token);

    var inputName = session.InputMetadata.Keys.First();
    var outputName = session.OutputMetadata.Keys.First();
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
        var input = ToInputTensor(bitmap, padded);

        using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
        var output = results.First(r => r.Name == outputName).AsTensor<float>();
        CopyTileInto(upscaled, output, tile, padded);
      }
    }

    var info = new SKImageInfo(bitmap.Width * OutputScale, bitmap.Height * OutputScale);
    var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
    return upscaled.Resize(info, sampling);
  }

  public void Dispose() => _Session?.Dispose();

  private static DenseTensor<float> ToInputTensor(SKBitmap bitmap, SKRectI area)
  {
    var tensor = new DenseTensor<float>([1, 3, area.Height, area.Width]);
    for (var y = 0; y < area.Height; y++)
    {
      for (var x = 0; x < area.Width; x++)
      {
        var pixel = bitmap.GetPixel(area.Left + x, area.Top + y);
        tensor[0, 0, y, x] = pixel.Red / 255f;
        tensor[0, 1, y, x] = pixel.Green / 255f;
        tensor[0, 2, y, x] = pixel.Blue / 255f;
      }
    }
    return tensor;
  }

  private static void CopyTileInto(SKBitmap destination, Tensor<float> output, SKRectI tile, SKRectI padded)
  {
    static byte Clamp(float v) => (byte)Math.Clamp(v * 255f, 0f, 255f);

    for (var y = tile.Top * ModelScale; y < tile.Bottom * ModelScale; y++)
    {
      var outputY = y - padded.Top * ModelScale;
      for (var x = tile.Left * ModelScale; x < tile.Right * ModelScale; x++)
      {
        var outputX = x - padded.Left * ModelScale;
        var red = Clamp(output[0, 0, outputY, outputX]);
        var green = Clamp(output[0, 1, outputY, outputX]);
        var blue = Clamp(output[0, 2, outputY, outputX]);
        var color = new SKColor(red, green, blue);
        destination.SetPixel(x, y, color);
      }
    }
  }
}
#endif
