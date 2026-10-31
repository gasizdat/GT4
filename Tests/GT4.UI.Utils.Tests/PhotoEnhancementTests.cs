using FluentAssertions;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Platforms.Windows;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class PhotoEnhancementTests : IDisposable
{
  private readonly FixtureModelCache _Models = new();

  public void Dispose() => _Models.Dispose();

  [Fact]
  public async Task Upscale_QuadruplesDimensions()
  {
    using var enhancement = new UpscalePhotoEnhancement(_Models.SessionFactory());
    _Models.Seed(enhancement.Models.Single(), "tiny-upscale-x4.onnx");
    using var photo = TestBitmap(20, 15);

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.Width.Should().Be(80);
    bitmap.Height.Should().Be(60);
  }

  [Fact]
  public async Task Colorize_KeepsSourceDimensions()
  {
    using var enhancement = new ColorizePhotoEnhancement(_Models.SessionFactory());
    _Models.Seed(enhancement.Models.Single(), "tiny-colorize.onnx");
    using var photo = TestBitmap(30, 20);

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.Width.Should().Be(30);
    bitmap.Height.Should().Be(20);
  }

  [Fact]
  public async Task Upscale_MissingModel_SurfacesModelDownloadException()
  {
    using var enhancement = new UpscalePhotoEnhancement(_Models.SessionFactory());
    using var photo = TestBitmap(10, 10);

    var act = () => enhancement.ApplyAsync(photo, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  private static SKBitmap TestBitmap(int width, int height)
  {
    var bitmap = new SKBitmap(width, height);
    bitmap.Erase(SKColors.Teal);
    return bitmap;
  }
}
