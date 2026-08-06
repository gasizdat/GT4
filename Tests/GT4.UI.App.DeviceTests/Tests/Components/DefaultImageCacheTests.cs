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
}
