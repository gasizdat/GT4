using FluentAssertions;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils.Platforms.Windows;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class RestorePhotoEnhancementTests : IDisposable
{
  private readonly FixtureModelCache _Models = new();

  public void Dispose() => _Models.Dispose();

  [Fact]
  public async Task HalfDamagedPhoto_OnlyReplacesTheDamagedHalfWithTheInpaintedConstant()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var photo = SKBitmap.Decode(HalfBlackHalfWhitePng(64, 48));

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.Width.Should().Be(64);
    bitmap.Height.Should().Be(48);
    // Left (black, clean under the sigmoid/0.4 threshold) stays exactly black.
    bitmap.GetPixel(4, 24).Should().Be(new SKColor(0, 0, 0));
    // Right (white, flagged as damaged) becomes the inpainter fixture's constant.
    bitmap.GetPixel(60, 24).Should().Be(new SKColor(128, 128, 128));
  }

  [Fact]
  public async Task HalfDamagedGrayscalePhoto_OnlyReplacesTheDamagedHalfWithTheInpaintedConstant()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var colour = SKBitmap.Decode(HalfBlackHalfWhitePng(64, 48));
    // A grayscale JPEG decodes as Gray8, one byte per pixel.
    using var photo = colour.Copy(SKColorType.Gray8);

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.GetPixel(4, 24).Should().Be(new SKColor(0, 0, 0));
    bitmap.GetPixel(60, 24).Should().Be(new SKColor(128, 128, 128));
  }

  [Fact]
  public async Task TranslucentPhoto_IsReadByItsColoursNotItsPremultipliedValues()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var photo = new SKBitmap(64, 48);
    photo.Erase(new SKColor(40, 60, 80, 128));
    // Stored premultiplied as 64/255 gray, under the identity detector's threshold.
    var translucentWhite = new SKColor(255, 255, 255, 64);
    for (var y = 0; y < 48; y++)
    {
      for (var x = 32; x < 64; x++)
      {
        photo.SetPixel(x, y, translucentWhite);
      }
    }
    var clean = photo.GetPixel(4, 24);

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.GetPixel(4, 24).Should().Be(clean);
    bitmap.GetPixel(60, 24).Should().Be(new SKColor(128, 128, 128));
  }

  [Fact]
  public async Task DamageOnlyPastTheFirstTile_IsInpaintedWhereItIs()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    // 600 wide, so the damage lies outside the first 512px tile.
    using var photo = SKBitmap.Decode(WhiteSquarePng(600, 40, x: 540, y: 10, size: 20));

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.GetPixel(550, 20).Should().Be(new SKColor(128, 128, 128));
    bitmap.GetPixel(100, 20).Should().Be(new SKColor(0, 0, 0));
    bitmap.GetPixel(590, 20).Should().Be(new SKColor(0, 0, 0));
  }

  [Theory]
  [InlineData(1100, 40)]
  [InlineData(40, 1100)]
  public async Task DamageAcrossTileEdges_IsFilledWithoutASeam(int width, int height)
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-mean.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    // The inpainter fills a tile with its mean colour, so tiles further along the dark gradient
    // fill the white band down its middle lighter.
    using var photo = SKBitmap.Decode(GradientWithBandPng(width, height));

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    var horizontal = width > height;
    var length = Math.Max(width, height);
    SKColor BandPixel(int i) => horizontal ? bitmap.GetPixel(i, height / 2) : bitmap.GetPixel(width / 2, i);
    var band = Enumerable.Range(0, length).Select(i => (int)BandPixel(i).Red).ToArray();
    var next = band.Skip(1);
    var steps = band.Zip(next, (previous, current) => Math.Abs(current - previous)).ToArray();
    steps.Max().Should().BeLessThanOrEqualTo(1);
  }

  [Theory]
  [InlineData(1, 16)]
  [InlineData(16, 1)]
  public async Task PhotoOnePixelAcross_IsInpaintedThroughItsReflectPadding(int width, int height)
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var photo = SKBitmap.Decode(SolidColorPng(width, height, 255, 255, 255));

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.Pixels.Should().OnlyContain(pixel => pixel == new SKColor(128, 128, 128));
  }

  [Theory]
  [InlineData(300, new[] { 0 })]
  [InlineData(512, new[] { 0 })]
  [InlineData(600, new[] { 0, 88 })]
  [InlineData(1426, new[] { 0, 304, 609, 914 })]
  public void TileOrigins_SpreadTilesEvenlyWithinThePhoto(int length, int[] expected)
  {
    RestorePhotoEnhancement.TileOrigins(length).Should().Equal(expected);
  }

  [Fact]
  public async Task ReleaseModels_ReleasesTheDetectorAndTheInpainter()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
    var provider = _Models.SessionProvider();
    var enhancement = new RestorePhotoEnhancement(provider);
    var detector = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    var inpainter = await provider.GetSessionAsync(RestorePhotoEnhancement.InpainterModel, CancellationToken.None);

    enhancement.ReleaseModels();

    var reloadedDetector = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    var reloadedInpainter = await provider.GetSessionAsync(RestorePhotoEnhancement.InpainterModel, CancellationToken.None);
    reloadedDetector.Should().NotBeSameAs(detector);
    reloadedInpainter.Should().NotBeSameAs(inpainter);
  }

  [Fact]
  public async Task MissingInpainterModel_SurfacesModelDownloadException()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var photo = SKBitmap.Decode(HalfBlackHalfWhitePng(64, 48));

    var act = () => enhancement.ApplyAsync(photo, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  [Fact]
  public async Task ThinDamagedLine_DilatesTheMaskBeyondTheDetectorsRawThreshold()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    var detector = await _Models.SessionProvider().GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    // 256x256 so the detector's working resolution maps 1:1 onto photo pixels -- the dilation's
    // 3-detector-pixel reach is then directly observable.
    using var bitmap = SKBitmap.Decode(HorizontalLinePng(256, 256, 128, 1));

    using var mask = RestorePhotoEnhancement.DetectDamageMask(detector, bitmap);

    // On the line: flagged directly by the detector's threshold.
    mask.GetPixel(128, 128).Should().Be(SKColors.White);
    // Within the 3-iteration dilation's reach: also flagged, though the detector's raw threshold
    // never touched these rows -- this is what distinguishes dilation from a plain threshold.
    mask.GetPixel(128, 125).Should().Be(SKColors.White);
    mask.GetPixel(128, 131).Should().Be(SKColors.White);
    // Outside the dilation's reach: untouched.
    mask.GetPixel(128, 120).Should().Be(SKColors.Black);
    mask.GetPixel(128, 136).Should().Be(SKColors.Black);
  }

  [Fact]
  public async Task LargePhoto_DilationMarginStaysBoundedRegardlessOfDownsampleRatio()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    var detector = await _Models.SessionProvider().GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    // 2048x2048 downsamples to a 256x256 detector input (an 8x ratio) -- large enough that
    // dilating in the detector's own low resolution before upsampling, instead of after, would
    // make the margin visibly wrong (8x too wide on each side) rather than just imprecise.
    // The 8px line fills exactly detector row 128's source block (rows 1024-1031).
    using var bitmap = SKBitmap.Decode(HorizontalLinePng(2048, 2048, 1024, 8));

    using var mask = RestorePhotoEnhancement.DetectDamageMask(detector, bitmap);

    // Mapping the detected row back covers its 8px source block; the 3-iteration dilation adds
    // exactly 3 more photo pixels on each side of that block, not 3 detector pixels' worth.
    mask.GetPixel(1024, 1021).Should().Be(SKColors.White);
    mask.GetPixel(1024, 1034).Should().Be(SKColors.White);
    mask.GetPixel(1024, 1020).Should().Be(SKColors.Black);
    mask.GetPixel(1024, 1035).Should().Be(SKColors.Black);
  }

  [Fact]
  public async Task OnePixelLinesOnALargePhoto_AreAveragedBelowTheThreshold()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    var detector = await _Models.SessionProvider().GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    // An 8x shrink that doesn't average reads row 1024 (corner-aligned) or row 1027 (centre-aligned,
    // nearest or linear) of each 8-row block. Averaged, the two lines are 2/8 white, under the
    // identity detector's threshold.
    using var bitmap = SKBitmap.Decode(HorizontalLinePng(2048, 2048, 1024, 1));
    using var canvas = new SKCanvas(bitmap);
    canvas.DrawRect(0, 1027, 2048, 1, new SKPaint { Color = SKColors.White });

    using var mask = RestorePhotoEnhancement.DetectDamageMask(detector, bitmap);

    mask.GetPixel(1024, 1024).Should().Be(SKColors.Black);
    mask.GetPixel(1024, 1027).Should().Be(SKColors.Black);
  }

  [Fact]
  public async Task UndamagedPhoto_IsRefusedWithoutLoadingTheInpainterModel()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    // No lama-fp32.onnx seeded, and the factory's downloader always fails -- if the enhancer
    // tried to load the inpainter for an undamaged photo, this would throw ModelDownloadException.
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var photo = SKBitmap.Decode(SolidColorPng(20, 16, 0, 0, 0));

    var act = () => enhancement.ApplyAsync(photo, CancellationToken.None);

    await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(UIStrings.ErrorPhotoHasNoDamage);
  }

  [Fact]
  public async Task MissingDetectorModel_SurfacesModelDownloadException()
  {
    var enhancement = new RestorePhotoEnhancement(_Models.SessionProvider());
    using var photo = SKBitmap.Decode(SolidColorPng(10, 10, 255, 255, 255));

    var act = () => enhancement.ApplyAsync(photo, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  [Fact]
  public void CompositeMaskedPixels_OnlyReplacesPixelsTheMaskMarks()
  {
    using var source = SolidColorBitmap(4, 1, 10, 20, 30);
    using var inpainted = SolidColorBitmap(4, 1, 200, 210, 220);
    using var mask = new SKBitmap(4, 1);
    mask.SetPixel(0, 0, SKColors.White);
    mask.SetPixel(1, 0, SKColors.Black);
    mask.SetPixel(2, 0, SKColors.White);
    mask.SetPixel(3, 0, SKColors.Black);
    using var destination = new SKBitmap(4, 1);

    RestorePhotoEnhancement.CompositeMaskedPixels(source, inpainted, mask, destination);

    destination.GetPixel(0, 0).Should().Be(new SKColor(200, 210, 220));
    destination.GetPixel(1, 0).Should().Be(new SKColor(10, 20, 30));
    destination.GetPixel(2, 0).Should().Be(new SKColor(200, 210, 220));
    destination.GetPixel(3, 0).Should().Be(new SKColor(10, 20, 30));
  }

  private static byte[] HalfBlackHalfWhitePng(int width, int height)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Black);
    surface.Canvas.DrawRect(width / 2f, 0, width / 2f, height, new SKPaint { Color = SKColors.White });
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static byte[] WhiteSquarePng(int width, int height, int x, int y, int size)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Black);
    surface.Canvas.DrawRect(x, y, size, size, new SKPaint { Color = SKColors.White });
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static byte[] GradientWithBandPng(int width, int height)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    var colors = new[] { SKColors.Black, new SKColor(70, 70, 70) };
    var horizontal = width > height;
    var end = horizontal ? new SKPoint(width, 0) : new SKPoint(0, height);
    using var gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0), end, colors, SKShaderTileMode.Clamp);
    surface.Canvas.DrawRect(0, 0, width, height, new SKPaint { Shader = gradient });
    var band = horizontal ? SKRect.Create(0, height / 2 - 5, width, 10) : SKRect.Create(width / 2 - 5, 0, 10, height);
    surface.Canvas.DrawRect(band, new SKPaint { Color = SKColors.White });
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static byte[] HorizontalLinePng(int width, int height, int lineY, int lineHeight)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Black);
    surface.Canvas.DrawRect(0, lineY, width, lineHeight, new SKPaint { Color = SKColors.White });
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static byte[] SolidColorPng(int width, int height, byte r, byte g, byte b)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(new SKColor(r, g, b));
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
  }

  private static SKBitmap SolidColorBitmap(int width, int height, byte r, byte g, byte b)
  {
    var bitmap = new SKBitmap(width, height);
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        bitmap.SetPixel(x, y, new SKColor(r, g, b));
      }
    }
    return bitmap;
  }
}
