using FluentAssertions;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Platforms.Windows;
using SkiaSharp;
using System.Globalization;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class PhotoEnhancementTests : IDisposable
{
  private readonly FixtureModelCache _Models = new();

  public void Dispose() => _Models.Dispose();

  [Fact]
  public async Task Upscale_PhotoSpanningSeveralTiles_MatchesANearestNeighbourUpscaleWithoutSeams()
  {
    using var enhancement = new UpscalePhotoEnhancement(_Models.SessionFactory());
    _Models.Seed(enhancement.Models.Single(), "tiny-upscale-x4.onnx");
    // 600x530 spans two 512px tiles each way, so every tile edge and partial tile is crossed.
    using var photo = PatternBitmap(600, 530);

    using var bitmap = await enhancement.ApplyAsync(photo, CancellationToken.None);

    bitmap.Width.Should().Be(2400);
    bitmap.Height.Should().Be(2120);
    var upscaled = bitmap.Pixels;
    var source = photo.Pixels;
    var mismatches = 0;
    for (var y = 0; y < bitmap.Height; y++)
    {
      for (var x = 0; x < bitmap.Width; x++)
      {
        if (upscaled[y * bitmap.Width + x] != source[y / 4 * photo.Width + x / 4])
        {
          mismatches++;
        }
      }
    }
    mismatches.Should().Be(0);
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

  [Fact]
  public async Task Upscale_PhotoOverOneMegapixel_IsRefusedBeforeTheModelLoads()
  {
    using var enhancement = new UpscalePhotoEnhancement(_Models.SessionFactory());
    using var photo = TestBitmap(1025, 1024);

    var act = () => enhancement.ApplyAsync(photo, CancellationToken.None);

    // No model is seeded, so loading one would throw ModelDownloadException instead.
    await act.Should().ThrowExactlyAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task Upscale_PhotoOfExactlyOneMegapixel_GoesOnToLoadTheModel()
  {
    using var enhancement = new UpscalePhotoEnhancement(_Models.SessionFactory());
    using var photo = TestBitmap(1024, 1024);

    var act = () => enhancement.ApplyAsync(photo, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  [Fact]
  public void DisplayNames_FollowTheUICulture()
  {
    // UIStrings.Culture is process-global and would race other classes; CurrentUICulture stays inside this test.
    var previous = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = new CultureInfo(Language.DE.Code);
    try
    {
      using var upscale = new UpscalePhotoEnhancement(_Models.SessionFactory());
      using var colorize = new ColorizePhotoEnhancement(_Models.SessionFactory());
      using var restore = new RestorePhotoEnhancement(_Models.SessionFactory());

      string[] names = [upscale.DisplayName, colorize.DisplayName, restore.DisplayName];

      names.Should().Equal("Hochskalieren", "Kolorieren", "Schäden beheben");
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  private static SKBitmap TestBitmap(int width, int height)
  {
    var bitmap = new SKBitmap(width, height);
    bitmap.Erase(SKColors.Teal);
    return bitmap;
  }

  // Prime moduli, so a pixel copied from a tile or padding width away never matches by coincidence.
  private static SKBitmap PatternBitmap(int width, int height)
  {
    var bitmap = new SKBitmap(width, height);
    for (var y = 0; y < height; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var color = new SKColor((byte)(x % 251), (byte)(y % 241), (byte)((x + y) % 239));
        bitmap.SetPixel(x, y, color);
      }
    }
    return bitmap;
  }
}
