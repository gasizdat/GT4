using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

/// <summary>
/// Resolves the image links MarkdownView inlines. A <c>media:&lt;id&gt;</c> id is looked up in the project
/// rather than in whatever media a host happens to have loaded, so a biography can reference any image in
/// the project; the image itself comes from the item's own keyed <see cref="IDataConverter"/>, so this
/// never touches a photo's bytes. An http(s) link is measured through the same path, which is what lets a
/// remote image be laid out at a size rather than merely fitted to its column.
/// </summary>
public sealed class InlineMediaProvider
{
  private readonly ICurrentProjectProvider _ProjectProvider;
  private readonly DataConverterResolver _ConverterResolver;
  private readonly ICancellationTokenProvider _TokenProvider;
  private readonly IHttpClientFactory _HttpClientFactory;

  public InlineMediaProvider(
    ICurrentProjectProvider projectProvider,
    DataConverterResolver converterResolver,
    ICancellationTokenProvider tokenProvider,
    IHttpClientFactory httpClientFactory)
  {
    _ProjectProvider = projectProvider;
    _ConverterResolver = converterResolver;
    _TokenProvider = tokenProvider;
    _HttpClientFactory = httpClientFactory;
  }

  public async Task<InlineMedia?> ResolveAsync(string link)
  {
    using var token = _TokenProvider.CreateShortOperationCancellationToken();
    if (MarkdownLinkUtils.TryParseMediaId(link, out var mediaId))
    {
      return await ProjectMediaAsync(mediaId, token);
    }

    // Every custom scheme in a biography ("media:", "person:", "attachment:") parses as an absolute Uri
    // too, so http/https is checked rather than absoluteness -- handing "media:12345678901" to an
    // HttpClient would throw on the scheme instead of rendering nothing.
    var isAbsolute = Uri.TryCreate(link, UriKind.Absolute, out var uri);
    var isRemote = isAbsolute && (uri!.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    return isRemote ? await RemoteMediaAsync(uri!, token) : null;
  }

  private async Task<InlineMedia?> ProjectMediaAsync(int mediaId, CancellationToken token)
  {
    var media = await _ProjectProvider.Project.Data.TryGetDataByIdAsync(mediaId, token);
    if (media is null || !media.IsInlineImage())
    {
      return null;
    }

    var content = await _ConverterResolver(media.Category).ToObjectAsync(media, token);
    var source = content switch
    {
      PhotoInfo photo => photo.Source,
      AttachmentInfo attachment => attachment.Image?.Source,
      _ => null
    };

    return source is null ? null : await MeasuredAsync(source, token);
  }

  // The platform loader fetches the image again to draw it -- and caches it -- so what this costs is one
  // extra fetch, not a payload held in memory: the bytes are read for their header and dropped.
  private async Task<InlineMedia> RemoteMediaAsync(Uri uri, CancellationToken token)
  {
    var source = ImageSource.FromUri(uri);
    try
    {
      return await MeasuredAsync(source, token);
    }
    catch (HttpRequestException ex)
    {
      // An unreachable image still renders -- the platform loader makes its own attempt, and only the
      // size is lost. Memoising the failure as "nothing here" would blank it on a passing network blip.
      System.Diagnostics.Debug.WriteLine(ex);
      return new InlineMedia(source, null);
    }
  }

  // Measured from the ImageSource rather than from the stored Data: a converter may have downsized what
  // it produced, and only the bytes actually being rendered carry the dimensions that lay it out. They
  // are read here and dropped -- MarkdownView is handed two numbers, never a payload.
  private async Task<InlineMedia> MeasuredAsync(ImageSource source, CancellationToken token)
  {
    var bytes = await ImageUtils.ToBytesAsync(source, _HttpClientFactory, token);
    var pixelSize = bytes is null ? null : ImageUtils.PixelSize(bytes);

    return new InlineMedia(source, pixelSize);
  }
}
