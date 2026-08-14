using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

/// <summary>
/// Builds the <see cref="InlineMediaResolver"/> a host binds to MarkdownView so a <c>media:&lt;id&gt;</c>
/// reference renders. The id is looked up in the project rather than in whatever media the host happens
/// to have loaded, so a biography can inline any image in the project; the image itself comes from the
/// item's own keyed <see cref="IDataConverter"/>, so this never touches a photo's bytes.
/// </summary>
public static class MediaSourceUtils
{
  public static InlineMediaResolver CreateResolver(
    ICurrentProjectProvider projectProvider,
    DataConverterResolver converterResolver,
    ICancellationTokenProvider tokenProvider,
    IHttpClientFactory httpClientFactory) =>
    async mediaId =>
    {
      using var token = tokenProvider.CreateShortOperationCancellationToken();
      var media = await projectProvider.Project.Data.TryGetDataByIdAsync(mediaId, token);
      if (media is null || !IsInlineImage(media))
      {
        return null;
      }

      var content = await converterResolver(media.Category).ToObjectAsync(media, token);
      var source = content switch
      {
        PhotoInfo photo => photo.Source,
        AttachmentInfo attachment => attachment.Image?.Source,
        _ => null
      };

      return source is null ? null : await MeasuredAsync(source, httpClientFactory, token);
    };

  // A photo is an image by construction (its MIME may even be null); an attachment only when its own
  // MIME type says so -- an attached scan is just as renderable as a photo.
  public static bool IsInlineImage(Data media) =>
    media.Category.IsPhoto() || media.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

  // Measured from the ImageSource rather than from the stored Data: a converter may have downsized what
  // it produced, and only the bytes actually being rendered carry the dimensions that lay it out. They
  // are read here and dropped -- MarkdownView is handed two numbers, never a payload.
  private static async Task<InlineMedia> MeasuredAsync(
    ImageSource source, IHttpClientFactory httpClientFactory, CancellationToken token)
  {
    var bytes = await ImageUtils.ToBytesAsync(source, httpClientFactory, token);
    var pixelSize = bytes is null ? null : ImageUtils.PixelSize(bytes);

    return new InlineMedia(source, pixelSize);
  }
}
