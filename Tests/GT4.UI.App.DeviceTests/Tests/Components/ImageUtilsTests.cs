using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ImageUtilsTests
{
  [Fact]
  public void ImageFromRawResource_SameResourceName_ReturnsSameInstance()
  {
    var first = ImageUtils.ImageFromRawResource("male_stub.png", null);
    var second = ImageUtils.ImageFromRawResource("male_stub.png", null);

    Assert.Same(first, second);
  }

  [Fact]
  public void ImageFromRawResource_DifferentResourceNames_ReturnsDifferentInstances()
  {
    var male = ImageUtils.ImageFromRawResource("male_stub.png", null);
    var female = ImageUtils.ImageFromRawResource("female_stub.png", null);

    Assert.NotSame(male, female);
  }

  [Fact]
  public void ImageFromRawResource_SameResourceAndMaxSize_ReturnsSameInstance()
  {
    var first = ImageUtils.ImageFromRawResource("family_stub.png", ImageUtils.ThumbnailSize);
    var second = ImageUtils.ImageFromRawResource("family_stub.png", ImageUtils.ThumbnailSize);

    Assert.Same(first, second);
  }

  [Fact]
  public void ImageFromRawResource_SameResourceDifferentMaxSize_ReturnsDifferentInstances()
  {
    var thumbnail = ImageUtils.ImageFromRawResource("project_icon.png", ImageUtils.ThumbnailSize);
    var fullSize = ImageUtils.ImageFromRawResource("project_icon.png", null);

    Assert.NotSame(thumbnail, fullSize);
  }

  [Fact]
  public async Task ImageFromRawResource_WithMaxSize_DownsizesTheImage()
  {
    var source = ImageUtils.ImageFromRawResource("male_stub.png", ImageUtils.ThumbnailSize);
    var streamSource = Assert.IsType<StreamImageSource>(source);
    await using var stream = await streamSource.Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);

    var size = ImageUtils.PixelSize(buffer.ToArray());

    Assert.NotNull(size);
    Assert.True(size!.Value.Width <= ImageUtils.ThumbnailSize);
    Assert.True(size.Value.Height <= ImageUtils.ThumbnailSize);
  }
}
