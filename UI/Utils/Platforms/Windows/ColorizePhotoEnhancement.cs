#if WINDOWS
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class ColorizePhotoEnhancement : IPhotoEnhancement, IDisposable
{
  private static readonly ModelDescriptor Model = new(
    "ddcolor-tiny-fp16.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v1/ddcolor-tiny-fp16.onnx"),
    "2653DA00DC15E54A45E5200B61DBF82EE9CEAF56B02BB9B9657569AC775E82E6");
  private const int ModelSize = 512;

  private readonly IModelSessionFactory _SessionFactory;
  private InferenceSession? _Session;

  public ColorizePhotoEnhancement(IModelSessionFactory sessionFactory)
  {
    _SessionFactory = sessionFactory;
  }

  public string DisplayName => "Colorize";

  public async Task<Data> ApplyAsync(Data photo, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var session = _Session ??= await _SessionFactory.LoadSessionAsync(Model, token);
    using var bitmap = SKBitmap.Decode(photo.Content) ?? throw new InvalidOperationException("Could not decode the photo.");
    var width = bitmap.Width;
    var height = bitmap.Height;

    var fullL = new float[height, width];
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var pixel = bitmap.GetPixel(x, y);
        var (l, _, _) = Lab.FromSRgb(pixel.Red / 255f, pixel.Green / 255f, pixel.Blue / 255f);
        fullL[y, x] = l;
      }
    }

    var inputName = session.InputMetadata.Keys.First();
    var outputName = session.OutputMetadata.Keys.First();
    var input = ToInputTensor(fullL, width, height);

    using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
    var ab = results.First(r => r.Name == outputName).AsTensor<float>();

    using var outBitmap = ToOutputBitmap(fullL, ab, width, height);
    using var image = SKImage.FromBitmap(outBitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);

    return photo with { Content = encoded.ToArray(), MimeType = "image/jpeg" };
  }

  public void Dispose() => _Session?.Dispose();

  private static DenseTensor<float> ToInputTensor(float[,] fullL, int width, int height)
  {
    var tensor = new DenseTensor<float>([1, 3, ModelSize, ModelSize]);
    for (var y = 0; y < ModelSize; y++)
    {
      for (var x = 0; x < ModelSize; x++)
      {
        var l = fullL[y * height / ModelSize, x * width / ModelSize];
        var (r, g, b) = Lab.ToSRgb(l, 0f, 0f);
        tensor[0, 0, y, x] = r;
        tensor[0, 1, y, x] = g;
        tensor[0, 2, y, x] = b;
      }
    }
    return tensor;
  }

  private static SKBitmap ToOutputBitmap(float[,] fullL, Tensor<float> ab, int width, int height)
  {
    var bitmap = new SKBitmap(width, height);
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var abX = x * ModelSize / width;
        var abY = y * ModelSize / height;
        var (r, g, b) = Lab.ToSRgb(fullL[y, x], ab[0, 0, abY, abX], ab[0, 1, abY, abX]);
        bitmap.SetPixel(x, y, new SKColor(
          (byte)Math.Clamp(r * 255f, 0f, 255f),
          (byte)Math.Clamp(g * 255f, 0f, 255f),
          (byte)Math.Clamp(b * 255f, 0f, 255f)));
      }
    }
    return bitmap;
  }
}
#endif
