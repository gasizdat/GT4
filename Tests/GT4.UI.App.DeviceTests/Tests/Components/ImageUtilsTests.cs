using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ImageUtilsTests
{
  [Fact]
  public void ImageFromRawResource_SameResourceName_ReturnsSameInstance()
  {
    var first = ImageUtils.ImageFromRawResource("male_stub.png");
    var second = ImageUtils.ImageFromRawResource("male_stub.png");

    Assert.Same(first, second);
  }

  [Fact]
  public void ImageFromRawResource_DifferentResourceNames_ReturnsDifferentInstances()
  {
    var male = ImageUtils.ImageFromRawResource("male_stub.png");
    var female = ImageUtils.ImageFromRawResource("female_stub.png");

    Assert.NotSame(male, female);
  }
}
