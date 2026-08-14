using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Converters;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class InlineMediaProviderTests
{
  private const int CacheSizeLimit = 1024 * 1024;

  // Mirrors the real DataConverterResolver wiring (GT4Services.cs/ServiceCollectionExtensions.cs)
  // closely enough to exercise the resolver's actual per-category dispatch.
  private static DataConverterResolver ConverterResolver() => category => category switch
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

  // The whole project's Data table stands behind the resolver, which is the point: nothing here filters
  // by what a host happens to have loaded.
  private static InlineMediaResolver Resolver(params Data[] stored)
  {
    var services = new TestServices();
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((int? id, CancellationToken _) => stored.FirstOrDefault(item => item.Id == id));

    var tokenProvider = services.Provider.GetRequiredService<ICancellationTokenProvider>();
    var provider = new InlineMediaProvider(
      services.CurrentProjectProvider.Object, ConverterResolver(), tokenProvider, Mock.Of<IHttpClientFactory>());

    return provider.ResolveAsync;
  }

  private static async Task<byte[]> ResolvedBytesAsync(InlineMedia? media)
  {
    Assert.NotNull(media);
    return await PhotoBytes.ReadAsync(media.Source);
  }

  [Fact]
  public async Task PlainPhoto_IsInlinedUnchanged()
  {
    var photo = new Data(1, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var media = await Resolver(photo)(1);

    Assert.Equal(new byte[] { 1, 2, 3 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task TaggedPhoto_StripsTheResidueEnvelope()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [4, 5, 6]);
    var photo = new Data(2, content, "image/jpeg", DataCategory.PersonMainPhotoTagged);

    var media = await Resolver(photo)(2);

    Assert.Equal(new byte[] { 4, 5, 6 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task ImageAttachment_IsInlinedLikeAPhoto()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.jpg\n", [7, 8, 9]);
    var attachment = new Data(3, content, "image/jpeg", DataCategory.PersonAttachment);

    var media = await Resolver(attachment)(3);

    Assert.Equal(new byte[] { 7, 8, 9 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task NonImageAttachment_IsExcluded()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.pdf\n", [7, 8, 9]);
    var attachment = new Data(4, content, "application/pdf", DataCategory.PersonAttachment);

    var media = await Resolver(attachment)(4);

    Assert.Null(media);
  }

  [Fact]
  public async Task PhotoWithoutAMimeType_IsStillInlined()
  {
    var photo = new Data(5, [1, 2, 3], null, DataCategory.PersonPhoto);

    var media = await Resolver(photo)(5);

    Assert.Equal(new byte[] { 1, 2, 3 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task UnknownId_ResolvesToNothing()
  {
    var photo = new Data(6, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var media = await Resolver(photo)(999);

    Assert.Null(media);
  }

  // The reason for resolving by id instead of intersecting the host's own media list: a biography can
  // reference a photo the host never loaded -- one belonging to another person, or to a family.
  [Fact]
  public async Task PhotoTheHostNeverLoaded_IsStillInlined()
  {
    var ownPhoto = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);
    var otherPersonsPhoto = new Data(11, [4, 5, 6], "image/png", DataCategory.PersonPhoto);

    var media = await Resolver(ownPhoto, otherPersonsPhoto)(11);

    Assert.Equal(new byte[] { 4, 5, 6 }, await ResolvedBytesAsync(media));
  }

  // The layout MarkdownView applies to an inline image comes from these dimensions -- without them
  // every inline image falls back to a plain column-width fit, silently dropping the "50%" a biography
  // can ask for. They are measured from the ImageSource, so a downsized one would report its own size.
  [Fact]
  public async Task InlinedPhoto_CarriesItsPixelSize()
  {
    var png = new byte[24];
    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
    "IHDR"u8.CopyTo(png.AsSpan(12));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 640);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 480);
    var photo = new Data(7, png, "image/png", DataCategory.PersonMainPhoto);

    var media = await Resolver(photo)(7);

    Assert.NotNull(media);
    Assert.Equal(new Size(640, 480), media.PixelSize);
  }
}
