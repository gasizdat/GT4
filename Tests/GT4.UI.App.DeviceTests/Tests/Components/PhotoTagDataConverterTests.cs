using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Http;
using Moq;
using System.Text;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers PhotoTagDataConverter.ToObjectAsync against a hand-built GedcomPhotoResidue envelope (the
/// internal Encode/DecodeAsync pair isn't visible from this assembly, so the envelope is constructed
/// directly in the documented [4-byte tag-length][UTF-8 tags][image bytes] layout) -- proving a tagged
/// photo decodes to just its image portion, matching what ImageDataConverter would produce for the
/// same raw bytes.
/// </summary>
public class PhotoTagDataConverterTests
{
  private static byte[] BuildEnvelope(string tagText, byte[] imageBytes)
  {
    var tagBytes = Encoding.UTF8.GetBytes(tagText);
    using var buffer = new MemoryStream();
    buffer.Write(BitConverter.GetBytes(tagBytes.Length));
    buffer.Write(tagBytes);
    buffer.Write(imageBytes);
    return buffer.ToArray();
  }

  private static async Task<byte[]> ReadBytesAsync(ImageSource source)
  {
    var streamSource = Assert.IsType<StreamImageSource>(source);
    var stream = await streamSource.Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    return buffer.ToArray();
  }

  [Fact]
  public async Task ToObjectAsync_returns_null_for_a_null_photo()
  {
    var result = await new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()).ToObjectAsync(null, CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task ToObjectAsync_decodes_just_the_image_portion()
  {
    byte[] image = [10, 20, 30, 40, 50];
    var content = BuildEnvelope("0 OBJE\n1 TITL A caption\n", image);
    var photo = new Data(1, content, "image/png", DataCategory.PersonMainPhotoTagged);

    var result = await new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()).ToObjectAsync(photo, CancellationToken.None);

    var imageSource = Assert.IsAssignableFrom<ImageSource>(result);
    Assert.Equal(image, await ReadBytesAsync(imageSource));
  }

  [Fact]
  public async Task ToObjectAsync_matches_ImageDataConverter_for_the_same_raw_bytes()
  {
    byte[] image = [1, 2, 3, 4, 5];
    var taggedPhoto = new Data(1, BuildEnvelope("0 OBJE\n1 TITL X\n", image), "image/png", DataCategory.PersonMainPhotoTagged);
    var plainPhoto = new Data(2, image, "image/png", DataCategory.PersonMainPhoto);

    var taggedResult = await new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()).ToObjectAsync(taggedPhoto, CancellationToken.None);
    var plainResult = await new ImageDataConverter(Mock.Of<IHttpClientFactory>()).ToObjectAsync(plainPhoto, CancellationToken.None);

    var taggedBytes = await ReadBytesAsync(Assert.IsAssignableFrom<ImageSource>(taggedResult));
    var plainBytes = await ReadBytesAsync(Assert.IsAssignableFrom<ImageSource>(plainResult));
    Assert.Equal(plainBytes, taggedBytes);
  }

  [Fact]
  public async Task FromObjectAsync_returns_null_for_null_input()
  {
    var result = await new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()).FromObjectAsync(null, CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task FromObjectAsync_encodes_plain_bytes_like_ImageDataConverter()
  {
    // A modification can never legitimately carry the old photo's tags (no editable-caption UI in this
    // pass), so it must encode identically to the plain converter -- PersonDataItem.ToDataAsync is what
    // then downgrades the resulting Data's Category from tagged to plain. A StreamImageSource (built the
    // same way ImageUtils.ImageFromBytes does) is used so the conversion doesn't depend on resolving a
    // real file from the test host's working directory.
    var image = GT4.UI.Utils.ImageUtils.ImageFromBytes([1, 2, 3, 4]);

    var taggedResult = await new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()).FromObjectAsync(image, CancellationToken.None);
    var plainResult = await new ImageDataConverter(Mock.Of<IHttpClientFactory>()).FromObjectAsync(image, CancellationToken.None);

    Assert.NotNull(taggedResult);
    Assert.NotNull(plainResult);
    Assert.Equal(plainResult.Content, taggedResult.Content);
    Assert.Equal(plainResult.MimeType, taggedResult.MimeType);
  }
}
