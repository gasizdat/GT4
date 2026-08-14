using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class MediaSourceUtilsTests
{
  private const int CacheSizeLimit = 1024 * 1024;

  // Mirrors the real DataConverterResolver wiring (GT4Services.cs/ServiceCollectionExtensions.cs)
  // closely enough to exercise BuildMediaSourcesAsync's actual per-category dispatch.
  private static DataConverterResolver Resolver() => category => category switch
  {
    DataCategory.PersonMainPhotoTagged or DataCategory.PersonPhotoTagged =>
      new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)),
    DataCategory.PersonAttachment or DataCategory.FamilyAttachment =>
      new AttachmentDataConverter(category, Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)),
    _ => new ImageDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit))
  };

  // Matches the [4-byte tag-length][UTF-8 GEDCOM tag text][image bytes] layout GedcomPhotoResidue
  // encodes; hand-built here since Encode itself isn't visible outside Core.Gedcom.
  private static byte[] BuildTaggedPhotoContent(string tagText, byte[] imageBytes)
  {
    var tagBytes = System.Text.Encoding.UTF8.GetBytes(tagText);
    using var buffer = new MemoryStream();
    buffer.Write(BitConverter.GetBytes(tagBytes.Length));
    buffer.Write(tagBytes);
    buffer.Write(imageBytes);
    return buffer.ToArray();
  }

  private static Task<IReadOnlyDictionary<int, PhotoInfo>> BuildAsync(Data[] media, string? biography) =>
    MediaSourceUtils.BuildMediaSourcesAsync(Resolver(), media, biography, TestContext.Current.CancellationToken);

  [Fact]
  public async Task PlainPhoto_IsInlinedUnchanged()
  {
    var photo = new Data(1, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var sources = await BuildAsync([photo], "![caption](media:1)");

    Assert.Equal(new byte[] { 1, 2, 3 }, await PhotoBytes.ReadAsync(sources[1]));
  }

  [Fact]
  public async Task TaggedPhoto_StripsTheResidueEnvelope()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [4, 5, 6]);
    var photo = new Data(2, content, "image/jpeg", DataCategory.PersonMainPhotoTagged);

    var sources = await BuildAsync([photo], "![caption](media:2)");

    Assert.Equal(new byte[] { 4, 5, 6 }, await PhotoBytes.ReadAsync(sources[2]));
  }

  [Fact]
  public async Task ImageAttachment_IsInlinedLikeAPhoto()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.jpg\n", [7, 8, 9]);
    var attachment = new Data(3, content, "image/jpeg", DataCategory.PersonAttachment);

    var sources = await BuildAsync([attachment], "![scan](media:3)");

    Assert.Equal(new byte[] { 7, 8, 9 }, await PhotoBytes.ReadAsync(sources[3]));
  }

  [Fact]
  public async Task NonImageAttachment_IsExcluded()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.pdf\n", [7, 8, 9]);
    var attachment = new Data(4, content, "application/pdf", DataCategory.PersonAttachment);

    var sources = await BuildAsync([attachment], "![scan](media:4)");

    Assert.Empty(sources);
  }

  [Fact]
  public async Task PhotoWithoutAMimeType_IsStillInlined()
  {
    var photo = new Data(5, [1, 2, 3], null, DataCategory.PersonPhoto);

    var sources = await BuildAsync([photo], "![caption](media:5)");

    Assert.Equal(new byte[] { 1, 2, 3 }, await PhotoBytes.ReadAsync(sources[5]));
  }

  [Fact]
  public async Task NotYetSavedPhoto_IsExcluded()
  {
    var photo = new Data(ElementId.NonCommittedId, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var sources = await BuildAsync([photo], null);

    Assert.Empty(sources);
  }

  [Fact]
  public async Task UnreferencedPhoto_IsExcluded()
  {
    var photo = new Data(6, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var sources = await BuildAsync([photo], "No media reference here.");

    Assert.Empty(sources);
  }

  // The layout MarkdownView applies to an inline image comes from the converted PhotoInfo, so the
  // dimensions have to survive the conversion -- without them every inline image falls back to a plain
  // column-width fit, silently dropping the "50%" a biography can ask for.
  [Fact]
  public async Task InlinedPhoto_CarriesItsPixelSize()
  {
    var png = new byte[24];
    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
    "IHDR"u8.CopyTo(png.AsSpan(12));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 640);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 480);
    var photo = new Data(7, png, "image/png", DataCategory.PersonMainPhoto);

    var sources = await BuildAsync([photo], "![caption](media:7)");

    Assert.Equal(new Size(640, 480), sources[7].PixelSize);
  }
}
