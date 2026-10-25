#if WINDOWS
using SkiaSharp;

namespace GT4.UI.Utils.Platforms.Windows;

internal readonly record struct SimilarityTransform(float Scale, float Theta, float Tx, float Ty)
{
  public (float X, float Y) Apply(float x, float y)
  {
    var cos = MathF.Cos(Theta);
    var sin = MathF.Sin(Theta);
    return (Scale * (cos * x - sin * y) + Tx, Scale * (sin * x + cos * y) + Ty);
  }

  public (float X, float Y) ApplyInverse(float x, float y)
  {
    var cos = MathF.Cos(-Theta);
    var sin = MathF.Sin(-Theta);
    var invScale = 1f / Scale;
    var rx = x - Tx;
    var ry = y - Ty;
    return (invScale * (cos * rx - sin * ry), invScale * (sin * rx + cos * ry));
  }
}

// Aligns a detected face to the 512x512 crop GFPGAN was trained on, and pastes a restored crop
// back. The template and border color are facexlib's (FaceRestoreHelper, MIT); the feather here
// is an analytic stand-in for facexlib's raster erode-then-blur mask, not a port of it -- fading by
// distance to the crop's edge reproduces the same soft seam without rasterizing a second mask
// bitmap and skips facexlib's face-parsing model (its own separate license) entirely.
internal static class FaceAligner
{
  public const int CropSize = 512;

  private static readonly (float X, float Y)[] Template =
  [
    (192.98138f, 239.94708f), // left eye
    (318.90277f, 240.1936f),  // right eye
    (256.63416f, 314.01935f), // nose
    (201.26117f, 371.41043f), // left mouth corner
    (313.08905f, 371.15118f), // right mouth corner
  ];

  private static readonly SKColor BorderColor = new(135, 133, 132);

  public static SimilarityTransform FitToTemplate(float[] landmarks)
  {
    var src = new (float X, float Y)[5];
    for (var i = 0; i < 5; i++)
    {
      src[i] = (landmarks[i * 2], landmarks[i * 2 + 1]);
    }

    var meanSrc = (X: src.Average(p => p.X), Y: src.Average(p => p.Y));
    var meanDst = (X: Template.Average(p => p.X), Y: Template.Average(p => p.Y));

    float sxx = 0, sxy = 0, syx = 0, syy = 0, normSrc = 0;
    for (var i = 0; i < 5; i++)
    {
      var px = src[i].X - meanSrc.X;
      var py = src[i].Y - meanSrc.Y;
      var qx = Template[i].X - meanDst.X;
      var qy = Template[i].Y - meanDst.Y;
      sxx += px * qx;
      sxy += px * qy;
      syx += py * qx;
      syy += py * qy;
      normSrc += px * px + py * py;
    }

    var theta = MathF.Atan2(sxy - syx, sxx + syy);
    var scale = MathF.Sqrt((sxy - syx) * (sxy - syx) + (sxx + syy) * (sxx + syy)) / normSrc;

    var cos = MathF.Cos(theta);
    var sin = MathF.Sin(theta);
    var tx = meanDst.X - scale * (cos * meanSrc.X - sin * meanSrc.Y);
    var ty = meanDst.Y - scale * (sin * meanSrc.X + cos * meanSrc.Y);

    return new SimilarityTransform(scale, theta, tx, ty);
  }

  public static SKBitmap ExtractCrop(SKBitmap photo, SimilarityTransform transform)
  {
    var crop = new SKBitmap(CropSize, CropSize);
    for (var cy = 0; cy < CropSize; cy++)
    {
      for (var cx = 0; cx < CropSize; cx++)
      {
        var (sx, sy) = transform.ApplyInverse(cx, cy);
        crop.SetPixel(cx, cy, SampleBilinear(photo, sx, sy));
      }
    }
    return crop;
  }

  public static void PasteBack(SKBitmap output, SKBitmap restoredCrop, SimilarityTransform transform)
  {
    for (var y = 0; y < output.Height; y++)
    {
      for (var x = 0; x < output.Width; x++)
      {
        var (cx, cy) = transform.Apply(x, y);
        if (cx is < 0 or >= CropSize || cy is < 0 or >= CropSize)
        {
          continue;
        }

        var mask = EdgeFeather(cx, cy);
        if (mask <= 0f)
        {
          continue;
        }

        var face = SampleBilinear(restoredCrop, cx, cy);
        var background = output.GetPixel(x, y);
        output.SetPixel(x, y, new SKColor(
          (byte)(mask * face.Red + (1 - mask) * background.Red),
          (byte)(mask * face.Green + (1 - mask) * background.Green),
          (byte)(mask * face.Blue + (1 - mask) * background.Blue)));
      }
    }
  }

  private static float EdgeFeather(float cx, float cy)
  {
    const float erodeMargin = 8f;
    const float featherWidth = 64f;
    var dist = MathF.Min(MathF.Min(cx, CropSize - cx), MathF.Min(cy, CropSize - cy));
    var t = Math.Clamp((dist - erodeMargin) / featherWidth, 0f, 1f);
    return t * t * (3f - 2f * t);
  }

  private static SKColor SampleBilinear(SKBitmap bitmap, float x, float y)
  {
    var x0 = (int)MathF.Floor(x);
    var y0 = (int)MathF.Floor(y);
    var fx = x - x0;
    var fy = y - y0;

    var c00 = SampleClamped(bitmap, x0, y0);
    var c10 = SampleClamped(bitmap, x0 + 1, y0);
    var c01 = SampleClamped(bitmap, x0, y0 + 1);
    var c11 = SampleClamped(bitmap, x0 + 1, y0 + 1);

    byte Lerp(byte a, byte b, byte c, byte d) => (byte)(
      (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy);

    return new SKColor(
      Lerp(c00.Red, c10.Red, c01.Red, c11.Red),
      Lerp(c00.Green, c10.Green, c01.Green, c11.Green),
      Lerp(c00.Blue, c10.Blue, c01.Blue, c11.Blue));
  }

  private static SKColor SampleClamped(SKBitmap bitmap, int x, int y) =>
    x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height ? BorderColor : bitmap.GetPixel(x, y);
}
#endif
