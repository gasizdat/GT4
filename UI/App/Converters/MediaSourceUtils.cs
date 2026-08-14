using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.UI.Converters;

public static class MediaSourceUtils
{
  // A photo is an image by construction (its MIME may even be null); an attachment only when its own
  // MIME type says so -- an attached scan is just as renderable as a photo.
  public static bool IsInlineImage(Data media) =>
    media.Category.IsPhoto() || media.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
}
