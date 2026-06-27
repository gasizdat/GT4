namespace GT4.Core.Gedcom;

/// <summary>
/// Maps between a GT4 photo's <see cref="Project.Dto.Data.MimeType"/> and the GEDCOM <c>OBJE</c>
/// <c>FORM</c> token. GT4 stores images with a MIME type (often <c>image/bmp</c>, sometimes the picked
/// file's own type, sometimes null); GEDCOM uses a short form token (<c>jpeg</c>, <c>png</c>, ...). The
/// mapping is a plain <c>image/</c> prefix swap, chosen so every value — including a null MIME — round-trips.
/// </summary>
internal static class GedcomMedia
{
  private const string ImagePrefix = "image/";

  public static string? ToForm(string? mimeType)
  {
    if (string.IsNullOrEmpty(mimeType))
      return null;

    return mimeType.StartsWith(ImagePrefix, StringComparison.OrdinalIgnoreCase)
      ? mimeType[ImagePrefix.Length..]
      : mimeType;
  }

  public static string? ToMimeType(string? form)
  {
    if (string.IsNullOrEmpty(form))
      return null;

    // A form value that already carries a "/" is a full MIME type the export passed through verbatim (a
    // non-image type), so it is returned as-is; a bare token is an image subtype to re-prefix.
    return form.Contains('/') ? form : ImagePrefix + form;
  }
}
