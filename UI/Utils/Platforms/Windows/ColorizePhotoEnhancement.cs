#if WINDOWS
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class ColorizePhotoEnhancement : IPhotoEnhancement, IModelDefinition
{
  private static readonly ModelDescriptor Model = new(
    "ddcolor-tiny-fp16.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v1/ddcolor-tiny-fp16.onnx"),
    "2653DA00DC15E54A45E5200B61DBF82EE9CEAF56B02BB9B9657569AC775E82E6",
    "DDColor",
    "Apache-2.0",
    135444402);
  private const int ModelSize = 512;

  private readonly IModelSessionProvider _SessionProvider;

  public ColorizePhotoEnhancement(IModelSessionProvider sessionProvider)
  {
    _SessionProvider = sessionProvider;
  }

  public string DisplayName => UIStrings.EnhancementColorize;

  public string CaptionMark => UIStrings.PhotoMarkColorize;

  public ModelDescriptor[] Models => [Model];

  public async Task<SKBitmap> ApplyAsync(SKBitmap bitmap, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var session = await _SessionProvider.GetSessionAsync(Model, token);
    var width = bitmap.Width;
    var height = bitmap.Height;

    var pixels = bitmap.Pixels;
    var fullL = new float[height, width];
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var pixel = pixels[y * width + x];
        var (l, _, _) = Lab.FromSRgb(pixel.Red / 255f, pixel.Green / 255f, pixel.Blue / 255f);
        fullL[y, x] = l;
      }
    }

    var inputName = session.InputMetadata.Keys.First();
    var outputName = session.OutputMetadata.Keys.First();
    var input = ToInputTensor(fullL, width, height);

    using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
    var ab = (DenseTensor<float>)results.First(r => r.Name == outputName).AsTensor<float>();

    return ToOutputBitmap(fullL, ab, width, height);
  }

  public void ReleaseModels() => _SessionProvider.Release(Models);

  private static DenseTensor<float> ToInputTensor(float[,] fullL, int width, int height)
  {
    const int Plane = ModelSize * ModelSize;

    var tensor = new DenseTensor<float>([1, 3, ModelSize, ModelSize]);
    var values = tensor.Buffer.Span;
    for (var y = 0; y < ModelSize; y++)
    {
      for (var x = 0; x < ModelSize; x++)
      {
        var l = fullL[y * height / ModelSize, x * width / ModelSize];
        var (r, g, b) = Lab.ToSRgb(l, 0f, 0f);
        var i = y * ModelSize + x;
        values[i] = r;
        values[Plane + i] = g;
        values[2 * Plane + i] = b;
      }
    }
    return tensor;
  }

  private static SKBitmap ToOutputBitmap(float[,] fullL, DenseTensor<float> ab, int width, int height)
  {
    var values = ab.Buffer.Span;
    var abWidth = ab.Dimensions[3];
    var plane = ab.Dimensions[2] * abWidth;
    var bitmap = new SKBitmap(width, height);
    var pixels = BitmapColors.Of(bitmap);
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var abX = x * ModelSize / width;
        var abY = y * ModelSize / height;
        var i = abY * abWidth + abX;
        var (r, g, b) = Lab.ToSRgb(fullL[y, x], values[i], values[plane + i]);
        pixels[y * width + x] = new SKColor(
          (byte)Math.Clamp(r * 255f, 0f, 255f),
          (byte)Math.Clamp(g * 255f, 0f, 255f),
          (byte)Math.Clamp(b * 255f, 0f, 255f));
      }
    }
    return bitmap;
  }
}
#endif
