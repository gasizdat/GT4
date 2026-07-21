namespace GT4.Core.Gedcom;

/// <summary>
/// Maps between a GT4 photo/attachment's <see cref="Project.Dto.Data.MimeType"/> and the GEDCOM
/// <c>OBJE</c> <c>FORM</c> token. GT4 stores media with a MIME type (often <c>image/bmp</c>, sometimes
/// the picked file's own type, sometimes null); GEDCOM uses a short form token (<c>jpeg</c>, <c>png</c>,
/// <c>pdf</c>, ...). The mapping is an <c>image/</c>/<c>application/</c> prefix swap, chosen so every
/// value — including a null MIME — round-trips.
/// </summary>
internal static class GedcomMedia
{
  private const string ImagePrefix = "image/";
  private const string ApplicationPrefix = "application/";

  // The image subtypes GT4 recognizes as a photo rather than a non-image attachment, matched against
  // either the OBJE FORM token or the FILE extension.
  public static readonly HashSet<string> ImageForms =
    new(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "webp" };

  public static string? ToForm(string? mimeType)
  {
    if (string.IsNullOrEmpty(mimeType))
      return null;

    if (mimeType.StartsWith(ImagePrefix, StringComparison.OrdinalIgnoreCase))
      return mimeType[ImagePrefix.Length..];
    if (mimeType.StartsWith(ApplicationPrefix, StringComparison.OrdinalIgnoreCase))
      return mimeType[ApplicationPrefix.Length..];

    return mimeType;
  }

  public static string? ToMimeType(string? form)
  {
    if (string.IsNullOrEmpty(form))
      return null;

    // A form value already carrying a "/" is a full MIME type passed through verbatim; a bare token is
    // an image subtype to re-prefix, or a non-image one (e.g. "pdf") to prefix as application/*.
    if (form.Contains('/'))
      return form;

    return (ImageForms.Contains(form) ? ImagePrefix : ApplicationPrefix) + form;
  }
}
