using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

public class DefaultImageCacheTests
{
  [Fact]
  public void Get_SameResourceName_ReturnsSameInstance()
  {
    var cache = new DefaultImageCache();

    var first = cache.Get("male_stub.png");
    var second = cache.Get("male_stub.png");

    Assert.Same(first, second);
  }

  [Fact]
  public void Get_DifferentResourceNames_ReturnsDifferentInstances()
  {
    var cache = new DefaultImageCache();

    var male = cache.Get("male_stub.png");
    var female = cache.Get("female_stub.png");

    Assert.NotSame(male, female);
  }

  [Fact]
  public async Task Get_ReturnsAStubDownsizedToPhotoMaxDecodeSize()
  {
    var cache = new DefaultImageCache();

    var source = cache.Get("male_stub.png");

    using var stream = await ((StreamImageSource)source).Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    var size = ImageUtils.PixelSize(buffer.ToArray())!.Value;
    Assert.True(Math.Max(size.Width, size.Height) <= ImageUtils.PhotoMaxDecodeSize);
  }
}
