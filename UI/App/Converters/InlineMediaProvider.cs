using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

/// <summary>
/// Resolves the <c>media:&lt;id&gt;</c> references MarkdownView inlines. The id is looked up in the
/// project rather than in whatever media a host happens to have loaded, so a biography can reference any
/// image in the project; the image itself comes from the item's own keyed <see cref="IDataConverter"/>,
/// so this never touches a photo's bytes.
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

  public async Task<InlineMedia?> ResolveAsync(int mediaId)
  {
    using var token = _TokenProvider.CreateShortOperationCancellationToken();
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
