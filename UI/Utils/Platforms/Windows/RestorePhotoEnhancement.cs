#if WINDOWS
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class RestorePhotoEnhancement : IPhotoEnhancement, IModelDefinition
{
  internal static readonly ModelDescriptor DetectorModel = new(
    "scratch-detector.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v2/scratch-detector.onnx"),
    "7DCB1C76001706F06480C3EDCFFCD085C3082B26B5E68E266F38316776B73D2D",
    "Bringing Old Photos Back to Life",
    "MIT",
    150622382);
  internal static readonly ModelDescriptor InpainterModel = new(
    "lama-fp32.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v2/lama-fp32.onnx"),
    "1FAEF5301D78DB7DDA502FE59966957EC4B79DD64E16F03ED96913C7A4EB68D6",
    "LaMa",
    "Apache-2.0",
    208044816);

  // BOPBTL's Global/detection.py scale_256 target size (short side 256, rounded to a multiple of
  // 16 -- the UNet's depth-4 downsampling requires it) and its 0.4 sigmoid threshold.
  private const int DetectorInputSize = 256;
  private const float DamageThreshold = 0.4f;

  // Carve/LaMa-ONNX's lama_fp32.onnx is exported at a fixed 512x512.
  private const int InpaintTileSize = 512;
  // Tiles overlap by at least this much, and each one's result fades out over it toward an edge
  // with more photo beyond: a fill near that edge was made without seeing across it.
  private const int InpaintTileOverlap = 128;

  // BOPBTL's own Global/test.py dilates the detector's raw per-pixel mask with a 3x3 kernel over
  // this many iterations before inpainting -- an undilated mask is a 1-detector-pixel-wide line,
  // and LaMa just interpolates that gap from its immediate neighbors, so the repair is there but
  // invisible. Applied at full photo resolution (not BOPBTL's own low detector resolution) so the
  // margin stays a few pixels regardless of photo size; dilating before the detector mask is
  // upsampled would scale the margin by the photo/detector downsample ratio instead.
  private const int MaskDilationIterations = 3;

  private readonly IModelSessionProvider _SessionProvider;

  public RestorePhotoEnhancement(IModelSessionProvider sessionProvider)
  {
    _SessionProvider = sessionProvider;
  }

  public string DisplayName => UIStrings.EnhancementRestore;

  public string CaptionMark => UIStrings.PhotoMarkRestore;

  public ModelDescriptor[] Models => [DetectorModel, InpainterModel];

  public async Task<SKBitmap> ApplyAsync(SKBitmap bitmap, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var detector = await _SessionProvider.GetSessionAsync(DetectorModel, token);
    using var mask = DetectDamageMask(detector, bitmap);
    if (!HasAnyDamage(mask))
    {
      throw new InvalidOperationException(UIStrings.ErrorPhotoHasNoDamage);
    }

    return await InpaintAsync(bitmap, mask, token);
  }

  public void ReleaseModels() => _SessionProvider.Release(Models);

  internal static SKBitmap DetectDamageMask(InferenceSession detector, SKBitmap bitmap)
  {
    var (dw, dh) = ShortSideTo(bitmap.Width, bitmap.Height, DetectorInputSize);
    // Averages like upstream's Pillow BICUBIC, whose kernel widens when shrinking. Skia's cubic
    // keeps a fixed kernel and aliases instead.
    var info = new SKImageInfo(dw, dh);
    var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
    using var resized = bitmap.Resize(info, sampling);
    var resizedPixels = resized.Pixels;
    var input = new DenseTensor<float>([1, 1, dh, dw]);
    var inputValues = input.Buffer.Span;
    for (var i = 0; i < dw * dh; i++)
    {
      var pixel = resizedPixels[i];
      var gray = (pixel.Red * 0.299f + pixel.Green * 0.587f + pixel.Blue * 0.114f) / 255f;
      inputValues[i] = gray * 2f - 1f;
    }

    var inputName = detector.InputMetadata.Keys.First();
    var outputName = detector.OutputMetadata.Keys.First();
    using var results = detector.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
    var logits = (DenseTensor<float>)results.First(r => r.Name == outputName).AsTensor<float>();
    var logitValues = logits.Buffer.Span;
    var logitWidth = logits.Dimensions[3];

    var damaged = new bool[bitmap.Height, bitmap.Width];
    for (var y = 0; y < bitmap.Height; y++)
    {
      var dy = Math.Min(dh - 1, y * dh / bitmap.Height);
      for (var x = 0; x < bitmap.Width; x++)
      {
        var dx = Math.Min(dw - 1, x * dw / bitmap.Width);
        damaged[y, x] = Sigmoid(logitValues[dy * logitWidth + dx]) >= DamageThreshold;
      }
    }
    damaged = DilateMask(damaged, bitmap.Width, bitmap.Height);

    var mask = new SKBitmap(bitmap.Width, bitmap.Height);
    var maskPixels = BitmapColors.Of(mask);
    for (var y = 0; y < bitmap.Height; y++)
    {
      for (var x = 0; x < bitmap.Width; x++)
      {
        maskPixels[y * bitmap.Width + x] = damaged[y, x] ? SKColors.White : SKColors.Black;
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

  private static bool HasAnyDamage(SKBitmap mask) => HasDamageIn(mask, 0, 0, mask.Width, mask.Height);

  private async Task<SKBitmap> InpaintAsync(SKBitmap bitmap, SKBitmap mask, CancellationToken token)
  {
    var inpainter = await _SessionProvider.GetSessionAsync(InpainterModel, token);

    var source = bitmap.Pixels;
    using var inpaintedFull = new SKBitmap(bitmap.Width, bitmap.Height);
    inpaintedFull.Pixels = source;
    var weights = new float[bitmap.Height, bitmap.Width];
    foreach (var tileY in TileOrigins(bitmap.Height))
    {
      foreach (var tileX in TileOrigins(bitmap.Width))
      {
        token.ThrowIfCancellationRequested();
        if (!TileHasDamage(mask, tileX, tileY))
        {
          continue;
        }

        using var tileResult = InpaintTile(inpainter, source, mask, tileX, tileY);
        BlendTileInto(inpaintedFull, weights, tileResult, tileX, tileY);
      }
    }

    var output = new SKBitmap(bitmap.Width, bitmap.Height);
    CompositeMaskedPixels(bitmap, inpaintedFull, mask, output);
    return output;
  }

  // Spread evenly so the last tile ends at the photo's edge, not past it in reflect padding.
  internal static IEnumerable<int> TileOrigins(int length)
  {
    var span = Math.Max(0, length - InpaintTileSize);
    var stride = InpaintTileSize - InpaintTileOverlap;
    var steps = (span + stride - 1) / stride;
    var divisor = Math.Max(1, steps);
    return Enumerable.Range(0, steps + 1).Select(i => i * span / divisor);
  }

  private static bool TileHasDamage(SKBitmap mask, int tileX, int tileY) => HasDamageIn(
    mask, tileX, tileY, Math.Min(tileX + InpaintTileSize, mask.Width), Math.Min(tileY + InpaintTileSize, mask.Height));

  private static bool HasDamageIn(SKBitmap mask, int x0, int y0, int x1, int y1)
  {
    var pixels = BitmapColors.Of(mask);
    for (var y = y0; y < y1; y++)
    {
      for (var x = x0; x < x1; x++)
      {
        if (pixels[y * mask.Width + x].Red != 0)
        {
          return true;
        }
      }
    }
    return false;
  }

  private static SKBitmap InpaintTile(InferenceSession inpainter, SKColor[] pixels, SKBitmap mask, int tileX, int tileY)
  {
    const int Plane = InpaintTileSize * InpaintTileSize;
    // lama_fp32.onnx's output is already scaled to 0-255 -- confirmed empirically (a known
    // all-255 channel in, all-255 channel out), unlike its [0,1]-normalized input.
    static byte ToByte(float v) => (byte)Math.Clamp(v, 0f, 255f);

    var image = new DenseTensor<float>([1, 3, InpaintTileSize, InpaintTileSize]);
    var maskTensor = new DenseTensor<float>([1, 1, InpaintTileSize, InpaintTileSize]);
    var imageValues = image.Buffer.Span;
    var maskValues = maskTensor.Buffer.Span;
    var maskPixels = BitmapColors.Of(mask);
    for (var y = 0; y < InpaintTileSize; y++)
    {
      var sourceY = ReflectIndex(tileY + y, mask.Height);
      for (var x = 0; x < InpaintTileSize; x++)
      {
        var sourceX = ReflectIndex(tileX + x, mask.Width);
        var sourceIndex = sourceY * mask.Width + sourceX;
        var pixel = pixels[sourceIndex];
        var i = y * InpaintTileSize + x;
        imageValues[i] = pixel.Red / 255f;
        imageValues[Plane + i] = pixel.Green / 255f;
        imageValues[2 * Plane + i] = pixel.Blue / 255f;
        maskValues[i] = maskPixels[sourceIndex].Red != 0 ? 1f : 0f;
      }
    }

    using var results = inpainter.Run([
      NamedOnnxValue.CreateFromTensor("image", image),
      NamedOnnxValue.CreateFromTensor("mask", maskTensor),
    ]);
    var output = (DenseTensor<float>)results.First(r => r.Name == "output").AsTensor<float>();
    var outputValues = output.Buffer.Span;

    var tile = new SKBitmap(InpaintTileSize, InpaintTileSize);
    var tilePixels = BitmapColors.Of(tile);
    for (var i = 0; i < Plane; i++)
    {
      tilePixels[i] = new SKColor(
        ToByte(outputValues[i]), ToByte(outputValues[Plane + i]), ToByte(outputValues[2 * Plane + i]));
    }
    return tile;
  }

  // A running weighted average; the source copy it starts from carries no weight.
  private static void BlendTileInto(SKBitmap destination, float[,] weights, SKBitmap tile, int tileX, int tileY)
  {
    var endY = Math.Min(tileY + InpaintTileSize, destination.Height);
    var endX = Math.Min(tileX + InpaintTileSize, destination.Width);
    var pixels = BitmapColors.Of(destination);
    var tilePixels = BitmapColors.Of(tile);
    for (var y = tileY; y < endY; y++)
    {
      var weightY = TileEdgeWeight(y - tileY, tileY, destination.Height);
      for (var x = tileX; x < endX; x++)
      {
        var weight = weightY * TileEdgeWeight(x - tileX, tileX, destination.Width);
        var total = weights[y, x] + weight;
        var share = weight / total;
        var index = y * destination.Width + x;
        var current = pixels[index];
        var added = tilePixels[(y - tileY) * InpaintTileSize + x - tileX];
        byte Mix(byte from, byte to) => (byte)MathF.Round(from + (to - from) * share);
        pixels[index] = new SKColor(
          Mix(current.Red, added.Red), Mix(current.Green, added.Green), Mix(current.Blue, added.Blue));
        weights[y, x] = total;
      }
    }
  }

  private static float TileEdgeWeight(int offset, int origin, int length)
  {
    var fromStart = origin > 0 ? offset + 1 : InpaintTileOverlap;
    var toEnd = origin + InpaintTileSize < length ? InpaintTileSize - offset : InpaintTileOverlap;
    var nearest = Math.Min(fromStart, toEnd);
    var distance = Math.Min(nearest, InpaintTileOverlap);
    return distance / (float)InpaintTileOverlap;
  }

  // Every unmasked pixel stays exactly the source pixel -- the invariant #506 calls for.
  internal static void CompositeMaskedPixels(SKBitmap source, SKBitmap inpainted, SKBitmap mask, SKBitmap destination)
  {
    var pixels = source.Pixels;
    var inpaintedPixels = BitmapColors.Of(inpainted);
    var maskPixels = BitmapColors.Of(mask);
    for (var i = 0; i < pixels.Length; i++)
    {
      if (maskPixels[i].Red != 0)
      {
        pixels[i] = inpaintedPixels[i];
      }
    }
    destination.Pixels = pixels;
  }

  private static (int Width, int Height) ShortSideTo(int width, int height, int shortSide)
  {
    var scale = (double)shortSide / Math.Min(width, height);
    return (RoundToMultipleOf16(width * scale), RoundToMultipleOf16(height * scale));
  }

  private static int RoundToMultipleOf16(double value)
  {
    var rounded = (int)Math.Round(value / 16.0) * 16;
    return Math.Max(16, rounded);
  }

  // Mirrors an out-of-range index back into [0, length) -- the standard reflect-boundary
  // formula, not a single-bounce approximation, since a source smaller than one inpaint tile
  // needs more than one reflection.
  private static int ReflectIndex(int i, int length)
  {
    var period = 2 * length;
    var m = ((i % period) + period) % period;
    return m < length ? m : period - 1 - m;
  }

  private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));
}
#endif
