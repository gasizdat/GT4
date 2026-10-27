#if WINDOWS
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class RestorePhotoEnhancement : IPhotoEnhancement, IDisposable
{
  private static readonly ModelDescriptor DetectorModel = new(
    "scratch-detector.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v2/scratch-detector.onnx"),
    "7DCB1C76001706F06480C3EDCFFCD085C3082B26B5E68E266F38316776B73D2D");
  private static readonly ModelDescriptor InpainterModel = new(
    "lama-fp32.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v2/lama-fp32.onnx"),
    "1FAEF5301D78DB7DDA502FE59966957EC4B79DD64E16F03ED96913C7A4EB68D6");

  // Global/detection_models matches BOPBTL's own scale_256 preprocessing (short side 256,
  // rounded to a multiple of 16 -- the UNet's depth-4 downsampling requires it) and its 0.4
  // sigmoid threshold.
  private const int DetectorInputSize = 256;
  private const float DamageThreshold = 0.4f;

  // Carve/LaMa-ONNX's lama_fp32.onnx is exported at a fixed 512x512.
  private const int InpaintTileSize = 512;

  // BOPBTL's own Global/test.py dilates the detector's raw per-pixel mask with a 3x3 kernel over
  // this many iterations before inpainting (its HR mode -- the regime that matches tiling at
  // native resolution instead of downsampling the whole photo first, like this pipeline does). An
  // undilated mask is a 1-detector-pixel-wide line; LaMa just interpolates that gap from its
  // immediate neighbors, so the repair is there but invisible.
  private const int MaskDilationIterations = 3;

  private readonly IModelSessionFactory _SessionFactory;
  private InferenceSession? _DetectorSession;
  private InferenceSession? _InpainterSession;

  public RestorePhotoEnhancement(IModelSessionFactory sessionFactory)
  {
    _SessionFactory = sessionFactory;
  }

  public string DisplayName => "Restore damage";

  public async Task<Data> ApplyAsync(Data photo, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var detector = _DetectorSession ??= await _SessionFactory.LoadSessionAsync(DetectorModel, token);
    using var bitmap = SKBitmap.Decode(photo.Content) ?? throw new InvalidOperationException("Could not decode the photo.");
    using var mask = DetectDamageMask(detector, bitmap);

    using var output = HasAnyDamage(mask) ? await InpaintAsync(bitmap, mask, token) : bitmap.Copy();

    using var image = SKImage.FromBitmap(output);
    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);

    return photo with { Content = encoded.ToArray(), MimeType = "image/jpeg" };
  }

  public void Dispose()
  {
    _DetectorSession?.Dispose();
    _InpainterSession?.Dispose();
  }

  private static SKBitmap DetectDamageMask(InferenceSession detector, SKBitmap bitmap)
  {
    var (dw, dh) = ShortSideTo(bitmap.Width, bitmap.Height, DetectorInputSize);
    var input = new DenseTensor<float>([1, 1, dh, dw]);
    for (var y = 0; y < dh; y++)
    {
      for (var x = 0; x < dw; x++)
      {
        var pixel = bitmap.GetPixel(x * bitmap.Width / dw, y * bitmap.Height / dh);
        var gray = (pixel.Red * 0.299f + pixel.Green * 0.587f + pixel.Blue * 0.114f) / 255f;
        input[0, 0, y, x] = gray * 2f - 1f;
      }
    }

    var inputName = detector.InputMetadata.Keys.First();
    var outputName = detector.OutputMetadata.Keys.First();
    using var results = detector.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
    var logits = results.First(r => r.Name == outputName).AsTensor<float>();

    var damaged = new bool[dh, dw];
    for (var y = 0; y < dh; y++)
    {
      for (var x = 0; x < dw; x++)
      {
        damaged[y, x] = Sigmoid(logits[0, 0, y, x]) >= DamageThreshold;
      }
    }
    damaged = DilateMask(damaged, dw, dh);

    var mask = new SKBitmap(bitmap.Width, bitmap.Height);
    for (var y = 0; y < bitmap.Height; y++)
    {
      for (var x = 0; x < bitmap.Width; x++)
      {
        var dy = Math.Min(dh - 1, y * dh / bitmap.Height);
        var dx = Math.Min(dw - 1, x * dw / bitmap.Width);
        mask.SetPixel(x, y, damaged[dy, dx] ? SKColors.White : SKColors.Black);
      }
    }
    return mask;
  }

  private static bool[,] DilateMask(bool[,] mask, int width, int height)
  {
    for (var i = 0; i < MaskDilationIterations; i++)
    {
      var grown = new bool[height, width];
      for (var y = 0; y < height; y++)
      {
        for (var x = 0; x < width; x++)
        {
          grown[y, x] = HasSetNeighbor(mask, x, y, width, height);
        }
      }
      mask = grown;
    }
    return mask;
  }

  private static bool HasSetNeighbor(bool[,] mask, int x, int y, int width, int height)
  {
    for (var ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
    {
      for (var nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
      {
        if (mask[ny, nx])
        {
          return true;
        }
      }
    }
    return false;
  }

  private static bool HasAnyDamage(SKBitmap mask)
  {
    for (var y = 0; y < mask.Height; y++)
    {
      for (var x = 0; x < mask.Width; x++)
      {
        if (mask.GetPixel(x, y).Red != 0)
        {
          return true;
        }
      }
    }
    return false;
  }

  private async Task<SKBitmap> InpaintAsync(SKBitmap bitmap, SKBitmap mask, CancellationToken token)
  {
    var inpainter = _InpainterSession ??= await _SessionFactory.LoadSessionAsync(InpainterModel, token);

    using var inpaintedFull = bitmap.Copy();
    for (var tileY = 0; tileY < bitmap.Height; tileY += InpaintTileSize)
    {
      for (var tileX = 0; tileX < bitmap.Width; tileX += InpaintTileSize)
      {
        token.ThrowIfCancellationRequested();
        if (!TileHasDamage(mask, tileX, tileY))
        {
          continue;
        }

        using var tileResult = InpaintTile(inpainter, bitmap, mask, tileX, tileY);
        CopyTileInto(inpaintedFull, tileResult, tileX, tileY);
      }
    }

    var output = new SKBitmap(bitmap.Width, bitmap.Height);
    CompositeMaskedPixels(bitmap, inpaintedFull, mask, output);
    return output;
  }

  private static bool TileHasDamage(SKBitmap mask, int tileX, int tileY)
  {
    var endY = Math.Min(tileY + InpaintTileSize, mask.Height);
    var endX = Math.Min(tileX + InpaintTileSize, mask.Width);
    for (var y = tileY; y < endY; y++)
    {
      for (var x = tileX; x < endX; x++)
      {
        if (mask.GetPixel(x, y).Red != 0)
        {
          return true;
        }
      }
    }
    return false;
  }

  private static SKBitmap InpaintTile(InferenceSession inpainter, SKBitmap bitmap, SKBitmap mask, int tileX, int tileY)
  {
    var image = new DenseTensor<float>([1, 3, InpaintTileSize, InpaintTileSize]);
    var maskTensor = new DenseTensor<float>([1, 1, InpaintTileSize, InpaintTileSize]);
    for (var y = 0; y < InpaintTileSize; y++)
    {
      var sourceY = ReflectIndex(tileY + y, bitmap.Height);
      for (var x = 0; x < InpaintTileSize; x++)
      {
        var sourceX = ReflectIndex(tileX + x, bitmap.Width);
        var pixel = bitmap.GetPixel(sourceX, sourceY);
        image[0, 0, y, x] = pixel.Red / 255f;
        image[0, 1, y, x] = pixel.Green / 255f;
        image[0, 2, y, x] = pixel.Blue / 255f;
        maskTensor[0, 0, y, x] = mask.GetPixel(sourceX, sourceY).Red != 0 ? 1f : 0f;
      }
    }

    using var results = inpainter.Run([
      NamedOnnxValue.CreateFromTensor("image", image),
      NamedOnnxValue.CreateFromTensor("mask", maskTensor),
    ]);
    var output = results.First(r => r.Name == "output").AsTensor<float>();

    var tile = new SKBitmap(InpaintTileSize, InpaintTileSize);
    for (var y = 0; y < InpaintTileSize; y++)
    {
      for (var x = 0; x < InpaintTileSize; x++)
      {
        // lama_fp32.onnx's output is already scaled to 0-255 -- confirmed empirically (a known
        // all-255 channel in, all-255 channel out), unlike its [0,1]-normalized input.
        byte ToByte(float v) => (byte)Math.Clamp(v, 0f, 255f);
        tile.SetPixel(x, y, new SKColor(
          ToByte(output[0, 0, y, x]), ToByte(output[0, 1, y, x]), ToByte(output[0, 2, y, x])));
      }
    }
    return tile;
  }

  private static void CopyTileInto(SKBitmap destination, SKBitmap tile, int tileX, int tileY)
  {
    var endY = Math.Min(tileY + InpaintTileSize, destination.Height);
    var endX = Math.Min(tileX + InpaintTileSize, destination.Width);
    for (var y = tileY; y < endY; y++)
    {
      for (var x = tileX; x < endX; x++)
      {
        destination.SetPixel(x, y, tile.GetPixel(x - tileX, y - tileY));
      }
    }
  }

  // Every unmasked pixel stays exactly the source pixel -- the invariant #506 calls for.
  internal static void CompositeMaskedPixels(SKBitmap source, SKBitmap inpainted, SKBitmap mask, SKBitmap destination)
  {
    for (var y = 0; y < source.Height; y++)
    {
      for (var x = 0; x < source.Width; x++)
      {
        destination.SetPixel(x, y, mask.GetPixel(x, y).Red != 0 ? inpainted.GetPixel(x, y) : source.GetPixel(x, y));
      }
    }
  }

  private static (int Width, int Height) ShortSideTo(int width, int height, int shortSide)
  {
    var scale = (double)shortSide / Math.Min(width, height);
    return (RoundToMultipleOf16(width * scale), RoundToMultipleOf16(height * scale));
  }

  private static int RoundToMultipleOf16(double value) => Math.Max(16, (int)Math.Round(value / 16.0) * 16);

  // Mirrors an out-of-range index back into [0, length) -- the standard reflect-boundary
  // formula, not a single-bounce approximation, since a source smaller than one inpaint tile
  // needs more than one reflection.
  private static int ReflectIndex(int i, int length)
  {
    if (length == 1)
    {
      return 0;
    }
    var period = 2 * length;
    var m = ((i % period) + period) % period;
    return m < length ? m : period - 1 - m;
  }

  private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));
}
#endif
