using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
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

  public InlineMediaProvider(
    ICurrentProjectProvider projectProvider,
    DataConverterResolver converterResolver,
    ICancellationTokenProvider tokenProvider)
  {
    _ProjectProvider = projectProvider;
    _ConverterResolver = converterResolver;
    _TokenProvider = tokenProvider;
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
    var photo = content switch
    {
      PhotoInfo inlined => inlined,
      AttachmentInfo attachment => attachment.Image,
      _ => null
    };

    // The dimensions come along from the converter, which read them off the header of the bytes it was
    // already holding: MarkdownView is handed two numbers, never a payload.
    return photo is null ? null : new InlineMedia(photo.Source, photo.PixelSize);
  }
}
