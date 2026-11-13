using FluentAssertions;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ImageCacheTests
{
  [Fact]
  public void ImageThatWontDecode_IsNotRetriedOnTheNextRequest()
  {
    var cache = new ImageCache(1024);
    var calls = 0;
    byte[]? Undecodable()
    {
      calls++;
      return null;
    }

    var first = cache.GetImage("photo", Undecodable);
    var second = cache.GetImage("photo", Undecodable);

    first.Should().BeNull();
    second.Should().BeNull();
    calls.Should().Be(1);
  }
}
