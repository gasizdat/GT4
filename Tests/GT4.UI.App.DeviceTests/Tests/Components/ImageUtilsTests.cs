using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ImageUtilsTests
{
  [Fact]
  public async Task DownsizedAsync_ShrinksALargeSourceToTheRequestedMaxSize()
  {
    var content = await ImageUtils.ToBytesAsync("male_stub.png", CancellationToken.None);
    var photo = new PhotoInfo(ImageUtils.ImageFromBytes(content!), "caption");

    var downsized = await ImageUtils.DownsizedAsync(photo, 64, CancellationToken.None);

    var size = ImageUtils.PixelSize(await ReadBytesAsync(downsized.Source))!.Value;
    Assert.True(Math.Max(size.Width, size.Height) <= 64);
    Assert.Equal("caption", downsized.Caption);
  }

  [Fact]
  public async Task DownsizedAsync_FallsBackToTheOriginalPhotoWhenTheBytesCannotBeDecoded()
  {
    var photo = new PhotoInfo(ImageUtils.ImageFromBytes([1, 2, 3]), "caption");

    var result = await ImageUtils.DownsizedAsync(photo, 64, CancellationToken.None);

    Assert.Same(photo, result);
  }

  private static async Task<byte[]> ReadBytesAsync(ImageSource source)
  {
    using var stream = await ((StreamImageSource)source).Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    return buffer.ToArray();
  }
}
