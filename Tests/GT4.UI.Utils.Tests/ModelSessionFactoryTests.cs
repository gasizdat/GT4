using FluentAssertions;
using GT4.UI.Utils.Platforms.Windows;
using SkiaSharp;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ModelSessionFactoryTests : IDisposable
{
  private readonly FixtureModelCache _Models = new();

  public ModelSessionFactoryTests()
  {
    _Models.Seed(RestorePhotoEnhancement.DetectorModel, "tiny-detector-identity.onnx");
    _Models.Seed(RestorePhotoEnhancement.InpainterModel, "tiny-inpainter-constant.onnx");
  }

  public void Dispose() => _Models.Dispose();

  [Fact]
  public async Task LoadSessionAsync_SameModelTwice_ReturnsTheLoadedSession()
  {
    var factory = _Models.SessionFactory();

    var first = await factory.LoadSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    var second = await factory.LoadSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);

    second.Should().BeSameAs(first);
  }

  [Fact]
  public async Task Release_DisposesTheSessionAndTheNextLoadOpensANewOne()
  {
    var factory = _Models.SessionFactory();
    var released = await factory.LoadSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    using var photo = new SKBitmap(16, 16);

    factory.Release([RestorePhotoEnhancement.DetectorModel]);

    var reloaded = await factory.LoadSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    reloaded.Should().NotBeSameAs(released);
    var act = () => RestorePhotoEnhancement.DetectDamageMask(released, photo);
    // ORT 1.20.1 throws NullReferenceException, not ObjectDisposedException, from a disposed session.
    act.Should().Throw<Exception>();
  }

  [Fact]
  public async Task Release_LeavesModelsItWasNotGivenLoaded()
  {
    var factory = _Models.SessionFactory();
    var kept = await factory.LoadSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    await factory.LoadSessionAsync(RestorePhotoEnhancement.InpainterModel, CancellationToken.None);

    factory.Release([RestorePhotoEnhancement.InpainterModel]);

    var again = await factory.LoadSessionAsync(RestorePhotoEnhancement.DetectorModel, CancellationToken.None);
    again.Should().BeSameAs(kept);
  }
}
