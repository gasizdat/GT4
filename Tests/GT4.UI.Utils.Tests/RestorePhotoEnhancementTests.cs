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
    var photo = new Data(1, HalfBlackHalfWhitePng(20, 16), "image/png", DataCategory.PersonPhoto);

    var result = await enhancement.ApplyAsync(photo, CancellationToken.None);

    result.MimeType.Should().Be("image/jpeg");
    using var bitmap = SKBitmap.Decode(result.Content);
    bitmap.Width.Should().Be(20);
    bitmap.Height.Should().Be(16);
    // Left (black, clean under the sigmoid/0.4 threshold) stays exactly black.
    bitmap.GetPixel(2, 8).Should().Be(new SKColor(0, 0, 0));
    // Right (white, flagged as damaged) becomes the inpainter fixture's constant.
    bitmap.GetPixel(17, 8).Should().Be(new SKColor(128, 128, 128));
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
