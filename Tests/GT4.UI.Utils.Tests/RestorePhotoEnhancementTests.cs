using System.Net;
using System.Reflection;
using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Platforms.Windows;
using Moq;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class RestorePhotoEnhancementTests : IDisposable
{
  private readonly string _Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
  private readonly TempFileSystem _FileSystem;
  private readonly FixedStorage _Storage;

  public RestorePhotoEnhancementTests()
  {
    _FileSystem = new TempFileSystem(_Root);
    _Storage = new FixedStorage(new DirectoryDescription(Environment.SpecialFolder.ApplicationData, ["models"]));
  }

  public void Dispose()
  {
    if (Directory.Exists(_Root))
    {
      Directory.Delete(_Root, recursive: true);
    }
  }

  [Fact]
  public async Task HalfDamagedPhoto_OnlyReplacesTheDamagedHalfWithTheInpaintedConstant()
  {
    SeedModel("scratch-detector.onnx", "tiny-detector-identity.onnx");
    SeedModel("lama-fp32.onnx", "tiny-inpainter-constant.onnx");
    using var enhancement = new RestorePhotoEnhancement(SessionFactory());
    // 64x48: the dilated boundary (a few pixels either side of x=32) stays inside its own JPEG
    // blocks, leaving the far sample points below unaffected by quality-85 ringing.
    var photo = new Data(1, HalfBlackHalfWhitePng(64, 48), "image/png", DataCategory.PersonPhoto);

    var result = await enhancement.ApplyAsync(photo, CancellationToken.None);

    result.MimeType.Should().Be("image/jpeg");
    using var bitmap = SKBitmap.Decode(result.Content);
    bitmap.Width.Should().Be(64);
    bitmap.Height.Should().Be(48);
    // Left (black, clean under the sigmoid/0.4 threshold) stays exactly black.
    bitmap.GetPixel(4, 24).Should().Be(new SKColor(0, 0, 0));
    // Right (white, flagged as damaged) becomes the inpainter fixture's constant.
    bitmap.GetPixel(60, 24).Should().Be(new SKColor(128, 128, 128));
  }

  [Fact]
  public async Task ThinDamagedLine_DilatesTheMaskBeyondTheDetectorsRawThreshold()
  {
    SeedModel("scratch-detector.onnx", "tiny-detector-identity.onnx");
    var detector = await SessionFactory().LoadSessionAsync(
      new ModelDescriptor("scratch-detector.onnx", new Uri("https://example.test/unused"), ""), CancellationToken.None);
    using var _ = detector;
    // 256x256 so the detector's working resolution maps 1:1 onto photo pixels -- the dilation's
    // 3-detector-pixel reach is then directly observable.
    using var bitmap = SKBitmap.Decode(HorizontalLinePng(256, 256, 128));

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
    SeedModel("scratch-detector.onnx", "tiny-detector-identity.onnx");
    var detector = await SessionFactory().LoadSessionAsync(
      new ModelDescriptor("scratch-detector.onnx", new Uri("https://example.test/unused"), ""), CancellationToken.None);
    using var _ = detector;
    // 2048x2048 downsamples to a 256x256 detector input (an 8x ratio) -- large enough that
    // dilating in the detector's own low resolution before upsampling, instead of after, would
    // make the margin visibly wrong (8x too wide on each side) rather than just imprecise.
    using var bitmap = SKBitmap.Decode(HorizontalLinePng(2048, 2048, 1024));

    using var mask = RestorePhotoEnhancement.DetectDamageMask(detector, bitmap);

    // The forward/backward nearest-neighbor mapping alone already blocks the single detected
    // detector row out to the full 8px source block (rows 1024-1031); the 3-iteration dilation
    // adds exactly 3 more photo pixels on each side of that block, not 3 detector pixels' worth.
    mask.GetPixel(1024, 1021).Should().Be(SKColors.White);
    mask.GetPixel(1024, 1034).Should().Be(SKColors.White);
    mask.GetPixel(1024, 1020).Should().Be(SKColors.Black);
    mask.GetPixel(1024, 1035).Should().Be(SKColors.Black);
  }

  [Fact]
  public async Task UndamagedPhoto_NeverLoadsTheInpainterModel()
  {
    SeedModel("scratch-detector.onnx", "tiny-detector-identity.onnx");
    // No lama-fp32.onnx seeded, and the factory's downloader always fails -- if the enhancer
    // tried to load the inpainter for an undamaged photo, this would throw.
    using var enhancement = new RestorePhotoEnhancement(SessionFactory());
    var photo = new Data(1, SolidColorPng(20, 16, 0, 0, 0), "image/png", DataCategory.PersonPhoto);

    var result = await enhancement.ApplyAsync(photo, CancellationToken.None);

    using var bitmap = SKBitmap.Decode(result.Content);
    bitmap.GetPixel(10, 8).Should().Be(new SKColor(0, 0, 0));
  }

  [Fact]
  public async Task MissingDetectorModel_SurfacesModelDownloadException()
  {
    using var enhancement = new RestorePhotoEnhancement(SessionFactory());
    var photo = new Data(1, SolidColorPng(10, 10, 255, 255, 255), "image/png", DataCategory.PersonPhoto);

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

  private IModelSessionFactory SessionFactory()
  {
    var factory = new Mock<IHttpClientFactory>();
    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new FailingHandler()));
    var downloader = new ModelDownloader(factory.Object, _FileSystem, _Storage);
    return new ModelSessionFactory(downloader, _FileSystem, _Storage);
  }

  private void SeedModel(string modelFileName, string fixtureFileName)
  {
    using var fixture = OpenFixture(fixtureFileName);
    _FileSystem.Copy(fixture, new FileDescription(_Storage.Models, modelFileName, null));
  }

  private static Stream OpenFixture(string fileName)
  {
    var assembly = Assembly.GetExecutingAssembly();
    var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".{fileName}", StringComparison.Ordinal));
    return assembly.GetManifestResourceStream(name)!;
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

  private static byte[] HorizontalLinePng(int width, int height, int lineY)
  {
    var info = new SKImageInfo(width, height);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Black);
    surface.Canvas.DrawRect(0, lineY, width, 1, new SKPaint { Color = SKColors.White });
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

  private sealed class FailingHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
  }
}
