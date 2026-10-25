#if WINDOWS
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

internal readonly record struct DetectedFace(float X1, float Y1, float W, float H, float[] Landmarks, float Score);

// YuNet (opencv/face_detection_yunet, MIT): fixed 640x640 BGR input, three anchor-free heads
// (cls/obj/bbox/kps) per stride in {8, 16, 32}. OpenCV ships this decode inside cv2.FaceDetectorYN
// rather than the ONNX graph itself, so it's ported here from opencv/objdetect's face_detect.cpp.
internal static class YuNetFaceDetector
{
  private const int InputSize = 640;
  private static readonly int[] Strides = [8, 16, 32];

  public static DetectedFace[] DetectFaces(InferenceSession session, SKBitmap photo, float scoreThreshold = 0.6f, float iouThreshold = 0.3f)
  {
    using var resized = photo.Resize(new SKImageInfo(InputSize, InputSize), SKSamplingOptions.Default) ?? throw new InvalidOperationException("Could not resize the photo.");
    var input = ToInputTensor(resized);

    using var results = session.Run([NamedOnnxValue.CreateFromTensor("input", input)]);
    var outputs = results.ToDictionary(r => r.Name, r => r.AsTensor<float>());

    var candidates = new List<DetectedFace>();
    foreach (var stride in Strides)
    {
      var cols = InputSize / stride;
      var rows = InputSize / stride;
      var cls = outputs[$"cls_{stride}"];
      var obj = outputs[$"obj_{stride}"];
      var bbox = outputs[$"bbox_{stride}"];
      var kps = outputs[$"kps_{stride}"];

      for (var r = 0; r < rows; r++)
      {
        for (var c = 0; c < cols; c++)
        {
          var idx = r * cols + c;
          var score = MathF.Sqrt(Math.Clamp(cls[0, idx, 0], 0f, 1f) * Math.Clamp(obj[0, idx, 0], 0f, 1f));
          if (score < scoreThreshold)
          {
            continue;
          }

          var cx = (c + bbox[0, idx, 0]) * stride;
          var cy = (r + bbox[0, idx, 1]) * stride;
          var w = MathF.Exp(bbox[0, idx, 2]) * stride;
          var h = MathF.Exp(bbox[0, idx, 3]) * stride;

          var landmarks = new float[10];
          for (var n = 0; n < 5; n++)
          {
            landmarks[n * 2] = (kps[0, idx, n * 2] + c) * stride;
            landmarks[n * 2 + 1] = (kps[0, idx, n * 2 + 1] + r) * stride;
          }

          candidates.Add(new DetectedFace(cx - w / 2, cy - h / 2, w, h, landmarks, score));
        }
      }
    }

    var scaleX = (float)photo.Width / InputSize;
    var scaleY = (float)photo.Height / InputSize;
    return Nms(candidates, iouThreshold)
      .Select(face => new DetectedFace(
        face.X1 * scaleX, face.Y1 * scaleY, face.W * scaleX, face.H * scaleY,
        face.Landmarks.Select((v, i) => v * (i % 2 == 0 ? scaleX : scaleY)).ToArray(),
        face.Score))
      .ToArray();
  }

  private static DenseTensor<float> ToInputTensor(SKBitmap resized)
  {
    var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
    for (var y = 0; y < InputSize; y++)
    {
      for (var x = 0; x < InputSize; x++)
      {
        var pixel = resized.GetPixel(x, y);
        tensor[0, 0, y, x] = pixel.Blue;
        tensor[0, 1, y, x] = pixel.Green;
        tensor[0, 2, y, x] = pixel.Red;
      }
    }
    return tensor;
  }

  private static List<DetectedFace> Nms(List<DetectedFace> candidates, float iouThreshold)
  {
    var sorted = candidates.OrderByDescending(f => f.Score).ToList();
    var kept = new List<DetectedFace>();
    while (sorted.Count > 0)
    {
      var best = sorted[0];
      kept.Add(best);
      sorted = sorted.Skip(1).Where(f => Iou(f, best) < iouThreshold).ToList();
    }
    return kept;
  }

  private static float Iou(DetectedFace a, DetectedFace b)
  {
    var x1 = Math.Max(a.X1, b.X1);
    var y1 = Math.Max(a.Y1, b.Y1);
    var x2 = Math.Min(a.X1 + a.W, b.X1 + b.W);
    var y2 = Math.Min(a.Y1 + a.H, b.Y1 + b.H);
    var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
    var union = a.W * a.H + b.W * b.H - intersection;
    return union <= 0 ? 0 : intersection / union;
  }
}
#endif
