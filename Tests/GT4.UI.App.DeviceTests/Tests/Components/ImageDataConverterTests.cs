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

  [Theory]
  [InlineData(ElementId.NonCommittedId)]
  [InlineData(20)]
  public async Task An_image_that_wont_decode_asked_for_at_a_max_size_comes_back_as_the_unreadable_stub(int id)
  {
    var unreadable = new Data(id, [1, 2, 3], "image/tiff", DataCategory.PersonAttachment);
    var thumbnail = new ImageDataWithMaxSize(unreadable, ImageUtils.ThumbnailSize);

    var converted = await CreateConverter().ToObjectAsync(thumbnail, CancellationToken.None);

    var photo = Assert.IsType<PhotoInfo>(converted);
    Assert.Same(ImageUtils.UnreadableImageStub, photo.Source);
  }

  // The platform's decoder may read what Skia can't, so the full-size image is never swapped for the stub.
  [Fact]
  public async Task An_image_that_wont_decode_asked_for_full_size_comes_back_as_its_own_bytes()
  {
    var unreadable = new Data(20, [1, 2, 3], "image/tiff", DataCategory.PersonAttachment);

    var converted = await CreateConverter().ToObjectAsync(unreadable, CancellationToken.None);

    var photo = Assert.IsType<PhotoInfo>(converted);
    Assert.Equal([1, 2, 3], await PhotoBytes.ReadAsync(photo.Source));
  }
}
