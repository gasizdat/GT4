using FluentAssertions;
using GT4.UI.Utils.Platforms.Windows;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ModelSessionProviderTests : IDisposable
{
  private readonly FixtureModelCache _Models = new();

  public ModelSessionProviderTests()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
  }

  public void Dispose() => _Models.Dispose();

  [Fact]
  public async Task GetSessionAsync_SameModelTwice_ReturnsTheLoadedSession()
  {
    var provider = _Models.SessionProvider();

    var first = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    var second = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);

    second.Should().BeSameAs(first);
  }

  [Fact]
  public async Task Release_DisposesTheSessionAndTheNextLoadOpensANewOne()
  {
    var provider = _Models.SessionProvider();
    var released = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    using var photo = new SKBitmap(16, 16);

    provider.Release([RestorePhotoEnhancement.DetectorModel]);

    var reloaded = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    reloaded.Should().NotBeSameAs(released);
    using var mask = RestorePhotoEnhancement.DetectDamageMask(reloaded, photo);
    var act = () => RestorePhotoEnhancement.DetectDamageMask(released, photo);
    // ORT 1.20.1 throws NullReferenceException, not ObjectDisposedException, from a disposed session.
    act.Should().Throw<Exception>();
  }

  [Fact]
  public async Task Release_LeavesModelsItWasNotGivenLoaded()
  {
    var provider = _Models.SessionProvider();
    var kept = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    await provider.GetSessionAsync(RestorePhotoEnhancement.InpainterModel, CancellationToken.None);

    provider.Release([RestorePhotoEnhancement.InpainterModel]);

    var again = await provider.GetSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    again.Should().BeSameAs(kept);
  }
}
