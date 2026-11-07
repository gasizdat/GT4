using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ImageDataConverterTests
{
  private const int CacheSizeLimit = 1024 * 1024;

  private static ImageDataConverter CreateConverter() => new(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit));

  private static async Task<Size?> ConvertedSizeAsync(Data data)
  {
    var converted = await CreateConverter().ToObjectAsync(data, CancellationToken.None);
    var photo = Assert.IsType<PhotoInfo>(converted);
    var bytes = await PhotoBytes.ReadAsync(photo.Source);
    return ImageUtils.PixelSize(bytes);
  }

  // An enhanced copy is uncommitted until the dialog saves it, and it is the row a thumbnail exists to show.
  [Fact]
  public async Task An_uncommitted_image_asked_for_at_a_max_size_comes_back_downsized()
  {
    var copy = new Data(ElementId.NonCommittedId, TestImages.Png(300, 200), "image/png", DataCategory.PersonAttachment);

    var size = await ConvertedSizeAsync(new ImageDataWithMaxSize(copy, ImageUtils.ThumbnailSize));

    Assert.Equal(new Size(100, 67), size);
  }

  [Fact]
  public async Task An_uncommitted_image_asked_for_without_a_max_size_comes_back_full_size()
  {
    var copy = new Data(ElementId.NonCommittedId, TestImages.Png(300, 200), "image/png", DataCategory.PersonAttachment);

    var size = await ConvertedSizeAsync(copy);

    Assert.Equal(new Size(300, 200), size);
  }
}
