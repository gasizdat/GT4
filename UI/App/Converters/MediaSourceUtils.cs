using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

/// <summary>
/// Builds the id-to-image map MarkdownView binds via its MediaSources property to inline-render a
/// <c>media:&lt;id&gt;</c> reference. Every image is produced by the item's own keyed
/// <see cref="IDataConverter"/>, so this never touches a photo's or attachment's bytes itself.
/// </summary>
public static class MediaSourceUtils
{
  // Only a committed photo has a stable id a stored "media:id" reference can target; retaining the bytes
  // of one the biography doesn't reference would serve no purpose (#211).
  public static async Task<IReadOnlyDictionary<int, PhotoInfo>> BuildMediaSourcesAsync(
    DataConverterResolver resolver, IEnumerable<Data> media, string? biography, CancellationToken token)
  {
    var referencedIds = MediaLinkUtils.ExtractReferencedIds(biography);
    var inlined = media.Where(item => item.Id != ElementId.NonCommittedId && referencedIds.Contains(item.Id) && IsInlineImage(item));
    var converted = await Task.WhenAll(inlined.Select(item => ToImageAsync(resolver, item, token)));

    return converted
      .Where(entry => entry.Value is not null)
      .ToDictionary(entry => entry.Key, entry => entry.Value!);
  }

  // A photo is an image by construction (its MIME may even be null); an attachment only when its own
  // MIME type says so -- an attached scan is just as renderable as a photo.
  public static bool IsInlineImage(Data media) =>
    media.Category.IsPhoto() || media.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

  private static async Task<KeyValuePair<int, PhotoInfo?>> ToImageAsync(
    DataConverterResolver resolver, Data media, CancellationToken token)
  {
    var content = await resolver(media.Category).ToObjectAsync(media, token);
    var image = content switch
    {
      PhotoInfo photo => photo,
      AttachmentInfo attachment => attachment.Image,
      _ => null
    };

    return KeyValuePair.Create(media.Id, image);
  }
}
