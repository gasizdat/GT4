#if WINDOWS
namespace GT4.UI.Utils.Platforms.Windows;

// CIE L*a*b* <-> sRGB, D65 white point. ColorizePhotoEnhancement runs DDColor in Lab space: L comes
// from the source photo, a/b come from the model.
internal static class Lab
{
  const float Xn = 0.95047f, Yn = 1.0f, Zn = 1.08883f;

  public static (float L, float a, float b) FromSRgb(float r, float g, float b)
  {
    var (x, y, z) = ToXyz(r, g, b);
    var fx = F(x / Xn); var fy = F(y / Yn); var fz = F(z / Zn);
    return (116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
  }

  public static (float r, float g, float b) ToSRgb(float l, float a, float bb)
  {
    var fy = (l + 16f) / 116f;
    var fx = fy + a / 500f;
    var fz = fy - bb / 200f;
    var x = Xn * FInv(fx);
    var y = Yn * FInv(fy);
    var z = Zn * FInv(fz);
    return FromXyz(x, y, z);
  }

  static (float x, float y, float z) ToXyz(float r, float g, float b)
  {
    var rl = InvGamma(r); var gl = InvGamma(g); var bl = InvGamma(b);
    var x = 0.4124564f * rl + 0.3575761f * gl + 0.1804375f * bl;
    var y = 0.2126729f * rl + 0.7151522f * gl + 0.0721750f * bl;
    var z = 0.0193339f * rl + 0.1191920f * gl + 0.9503041f * bl;
    return (x, y, z);
  }

  static (float r, float g, float b) FromXyz(float x, float y, float z)
  {
    var rl = 3.2404542f * x - 1.5371385f * y - 0.4985314f * z;
    var gl = -0.9692660f * x + 1.8760108f * y + 0.0415560f * z;
    var bl = 0.0556434f * x - 0.2040259f * y + 1.0572252f * z;
    return (Gamma(Math.Clamp(rl, 0f, 1f)), Gamma(Math.Clamp(gl, 0f, 1f)), Gamma(Math.Clamp(bl, 0f, 1f)));
  }

  static float InvGamma(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
  static float Gamma(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

  const float Eps = 0.008856f, Kappa = 903.3f;
  static float F(float t) => t > Eps ? MathF.Cbrt(t) : (Kappa * t + 16f) / 116f;
  static float FInv(float t) => t > 6f / 29f ? t * t * t : 3f * (6f / 29f) * (6f / 29f) * (t - 4f / 29f);
}
#endif
