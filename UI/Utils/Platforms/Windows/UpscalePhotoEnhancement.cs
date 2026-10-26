#if WINDOWS
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

public class UpscalePhotoEnhancement : IPhotoEnhancement, IDisposable
{
  private static readonly ModelDescriptor Model = new(
    "realesr-general-x4v3.onnx",
    new Uri("https://github.com/gasizdat/GT4/releases/download/models-v1/realesr-general-x4v3.onnx"),
    "924EBAD6532777303582D4CE7811849B88869231A3AE7093E0F21200249DF8D5");

  private readonly IModelSessionFactory _SessionFactory;
  private InferenceSession? _Session;

  public UpscalePhotoEnhancement(IModelSessionFactory sessionFactory)
  {
    _SessionFactory = sessionFactory;
  }

  public string DisplayName => "Upscale";

  public async Task<Data> ApplyAsync(Data photo, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var session = _Session ??= await _SessionFactory.LoadSessionAsync(Model, token);
    using var bitmap = SKBitmap.Decode(photo.Content) ?? throw new InvalidOperationException("Could not decode the photo.");

    var inputName = session.InputMetadata.Keys.First();
    var outputName = session.OutputMetadata.Keys.First();
    var input = ToInputTensor(bitmap);

    using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
    var output = results.First(r => r.Name == outputName).AsTensor<float>();

    using var outBitmap = ToOutputBitmap(output);
    using var image = SKImage.FromBitmap(outBitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);

    return photo with { Content = encoded.ToArray(), MimeType = "image/jpeg" };
  }

  public void Dispose() => _Session?.Dispose();

  private static DenseTensor<float> ToInputTensor(SKBitmap bitmap)
  {
    var tensor = new DenseTensor<float>([1, 3, bitmap.Height, bitmap.Width]);
    for (var y = 0; y < bitmap.Height; y++)
    {
      for (var x = 0; x < bitmap.Width; x++)
      {
        var pixel = bitmap.GetPixel(x, y);
        tensor[0, 0, y, x] = pixel.Red / 255f;
        tensor[0, 1, y, x] = pixel.Green / 255f;
        tensor[0, 2, y, x] = pixel.Blue / 255f;
      }
    }
    return tensor;
  }

  private static SKBitmap ToOutputBitmap(Tensor<float> output)
  {
    var height = output.Dimensions[2];
    var width = output.Dimensions[3];
    var bitmap = new SKBitmap(width, height);
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        byte Clamp(float v) => (byte)Math.Clamp(v * 255f, 0f, 255f);
        bitmap.SetPixel(x, y, new SKColor(Clamp(output[0, 0, y, x]), Clamp(output[0, 1, y, x]), Clamp(output[0, 2, y, x])));
      }
    }
    return bitmap;
  }
}
#endif
