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
  private static InlineMediaResolver Resolver(params Data[] stored) =>
    Resolver(Mock.Of<IHttpClientFactory>(), stored);

  private static InlineMediaResolver Resolver(IHttpClientFactory httpClientFactory, params Data[] stored)
  {
    var services = new TestServices();
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((int? id, CancellationToken _) => stored.FirstOrDefault(item => item.Id == id));

    var tokenProvider = services.Provider.GetRequiredService<ICancellationTokenProvider>();
    var provider = new InlineMediaProvider(
      services.CurrentProjectProvider.Object, ConverterResolver(), tokenProvider, httpClientFactory);

    return provider.ResolveAsync;
  }

  // Serves the bytes to whatever URL is asked for, so a remote image can be measured without a network.
  private static IHttpClientFactory HttpServing(byte[] content)
  {
    var handler = new StubHttpMessageHandler(content);
    var factory = new Mock<IHttpClientFactory>();
    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
    return factory.Object;
  }

  private sealed class StubHttpMessageHandler : HttpMessageHandler
  {
    private readonly byte[] _Content;

    public StubHttpMessageHandler(byte[] content) => _Content = content;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(_Content) });
  }

  private sealed class FailingHttpMessageHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      throw new HttpRequestException("Unreachable.");
  }

  // Just the signature and the IHDR dimensions: ImageUtils.PixelSize reads only the header, so the rest
  // of a real PNG would add nothing these tests could assert on.
  private static byte[] PngHeader(int width, int height)
  {
    var png = new byte[24];
    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
    "IHDR"u8.CopyTo(png.AsSpan(12));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), (uint)width);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), (uint)height);
    return png;
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

    var media = await Resolver(photo)("media:1");

    Assert.Equal(new byte[] { 1, 2, 3 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task TaggedPhoto_StripsTheResidueEnvelope()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [4, 5, 6]);
    var photo = new Data(2, content, "image/jpeg", DataCategory.PersonMainPhotoTagged);

    var media = await Resolver(photo)("media:2");

    Assert.Equal(new byte[] { 4, 5, 6 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task ImageAttachment_IsInlinedLikeAPhoto()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.jpg\n", [7, 8, 9]);
    var attachment = new Data(3, content, "image/jpeg", DataCategory.PersonAttachment);

    var media = await Resolver(attachment)("media:3");

    Assert.Equal(new byte[] { 7, 8, 9 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task NonImageAttachment_IsExcluded()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.pdf\n", [7, 8, 9]);
    var attachment = new Data(4, content, "application/pdf", DataCategory.PersonAttachment);

    var media = await Resolver(attachment)("media:4");

    Assert.Null(media);
  }

  [Fact]
  public async Task PhotoWithoutAMimeType_IsStillInlined()
  {
    var photo = new Data(5, [1, 2, 3], null, DataCategory.PersonPhoto);

    var media = await Resolver(photo)("media:5");

    Assert.Equal(new byte[] { 1, 2, 3 }, await ResolvedBytesAsync(media));
  }

  [Fact]
  public async Task UnknownId_ResolvesToNothing()
  {
    var photo = new Data(6, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var media = await Resolver(photo)("media:999");

    Assert.Null(media);
  }

  // The reason for resolving by id instead of intersecting the host's own media list: a biography can
  // reference a photo the host never loaded -- one belonging to another person, or to a family.
  [Fact]
  public async Task PhotoTheHostNeverLoaded_IsStillInlined()
  {
    var ownPhoto = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);
    var otherPersonsPhoto = new Data(11, [4, 5, 6], "image/png", DataCategory.PersonPhoto);

    var media = await Resolver(ownPhoto, otherPersonsPhoto)("media:11");

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

    var media = await Resolver(photo)("media:7");

    Assert.NotNull(media);
    Assert.Equal(new Size(640, 480), media.PixelSize);
  }

  // A URL is fetched for its header alone: without those dimensions MarkdownView has nothing to take a
  // percentage of, which is why "![x 50%](https://...)" used to render at plain column fit.
  [Fact]
  public async Task RemoteImage_IsMeasuredOverHttp()
  {
    var png = PngHeader(800, 600);

    var media = await Resolver(HttpServing(png))("https://example.test/photo.png");

    Assert.NotNull(media);
    Assert.Equal(new Size(800, 600), media.PixelSize);
    Assert.IsType<UriImageSource>(media.Source);
  }

  // The platform loader still gets its own attempt, so an image that merely failed to be measured is
  // rendered unscaled rather than remembered as "nothing here" by a view that never asks twice.
  [Fact]
  public async Task RemoteImageThatCannotBeFetched_StillResolvesWithoutASize()
  {
    var factory = new Mock<IHttpClientFactory>();
    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new FailingHttpMessageHandler()));

    var media = await Resolver(factory.Object)("https://example.test/gone.png");

    Assert.NotNull(media);
    Assert.Null(media.PixelSize);
  }

  // Every custom scheme parses as an absolute Uri, so an id that overflows an int must not fall through
  // to the remote arm -- an HttpClient handed "media:99999999999" throws on the scheme.
  [Theory]
  [InlineData("media:99999999999")]
  [InlineData("person:5")]
  [InlineData("mailto:someone@example.test")]
  [InlineData("./relative/photo.png")]
  public async Task ALinkThatNamesNoImage_ResolvesToNothing(string link)
  {
    var photo = new Data(12, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var media = await Resolver(photo)(link);

    Assert.Null(media);
  }
}
