#if WINDOWS
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Platforms.Windows;

public class RestorePhotoEnhancement : IPhotoEnhancement
{
  private const string DetectorFileName = "yunet-2023mar.onnx";
  private const string RestorerFileName = "gfpgan-v1.4.onnx";

  private readonly IStorage _Storage;
  private readonly IFileSystem _FileSystem;
  private InferenceSession? _DetectorSession;
  private InferenceSession? _RestorerSession;

  public RestorePhotoEnhancement(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

  public string DisplayName => "Restore faces";

  public Task<Data> ApplyAsync(Data photo, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    var detector = _DetectorSession ??= LoadSession(DetectorFileName, loadFromMemory: true);
    using var bitmap = SKBitmap.Decode(photo.Content) ?? throw new InvalidOperationException("Could not decode the photo.");

    var faces = YuNetFaceDetector.DetectFaces(detector, bitmap);
    if (faces.Length == 0)
    {
      throw new InvalidOperationException("No face was found to restore.");
    }

    // GFPGAN's weights are ~340MB; unlike the smaller detector, load it from its own file path
    // instead of buffering the whole file into a byte[] first.
    var restorer = _RestorerSession ??= LoadSession(RestorerFileName, loadFromMemory: false);

    using var output = new SKBitmap(bitmap.Width, bitmap.Height);
    bitmap.CopyTo(output);

    foreach (var face in faces)
    {
      token.ThrowIfCancellationRequested();
      var transform = FaceAligner.FitToTemplate(face.Landmarks);
      using var crop = FaceAligner.ExtractCrop(bitmap, transform);
      using var restoredCrop = Restore(restorer, crop);
      FaceAligner.PasteBack(output, restoredCrop, transform);
    }

    using var image = SKImage.FromBitmap(output);
    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);

    return Task.FromResult(photo with { Content = encoded.ToArray(), MimeType = "image/jpeg" });
  }

  private InferenceSession LoadSession(string fileName, bool loadFromMemory)
  {
    var file = new FileDescription(_Storage.Models, fileName, null);
    if (!loadFromMemory)
    {
      return new InferenceSession(_FileSystem.ToPath(file));
    }

    using var stream = _FileSystem.OpenReadStream(file);
    using var modelBytes = new MemoryStream();
    stream.CopyTo(modelBytes);
    return new InferenceSession(modelBytes.ToArray());
  }

  private static SKBitmap Restore(InferenceSession session, SKBitmap crop)
  {
    var input = new DenseTensor<float>([1, 3, FaceAligner.CropSize, FaceAligner.CropSize]);
    for (var y = 0; y < FaceAligner.CropSize; y++)
    {
      for (var x = 0; x < FaceAligner.CropSize; x++)
      {
        var pixel = crop.GetPixel(x, y);
        input[0, 0, y, x] = pixel.Red / 127.5f - 1f;
        input[0, 1, y, x] = pixel.Green / 127.5f - 1f;
        input[0, 2, y, x] = pixel.Blue / 127.5f - 1f;
      }
    }

    using var results = session.Run([NamedOnnxValue.CreateFromTensor("input", input)]);
    var output = results.First(r => r.Name == "output").AsTensor<float>();

    var restored = new SKBitmap(FaceAligner.CropSize, FaceAligner.CropSize);
    for (var y = 0; y < FaceAligner.CropSize; y++)
    {
      for (var x = 0; x < FaceAligner.CropSize; x++)
      {
        byte ToByte(float v) => (byte)Math.Clamp((v + 1f) * 127.5f, 0f, 255f);
        restored.SetPixel(x, y, new SKColor(
          ToByte(output[0, 0, y, x]), ToByte(output[0, 1, y, x]), ToByte(output[0, 2, y, x])));
      }
    }
    return restored;
  }
}
#endif
