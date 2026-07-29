using System.Net.Mime;

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

  /// <summary>
  /// The form token to export stored media under. <c>application/octet-stream</c> is what the file picker
  /// stamps when it cannot name a type, so it names no format either: the bytes are asked first and the
  /// placeholder kept only when they say nothing.
  /// </summary>
  public static string? ResolveForm(string? mimeType, byte[] content)
  {
    if (string.Equals(mimeType, MediaTypeNames.Application.Octet, StringComparison.OrdinalIgnoreCase))
      return SniffForm(content) ?? ToForm(mimeType);

    return ToForm(mimeType) ?? SniffForm(content);
  }

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

  /// <summary>
  /// The form token the bytes themselves declare, for a photo GT4 stored without a MIME type. An embedded
  /// BLOB could be left unlabelled -- import treats a formless BLOB as an image -- but an external FILE has
  /// only its extension and FORM to go on, so an unlabelled photo would reimport as a plain attachment.
  /// </summary>
  public static string? SniffForm(byte[] content) => content switch
  {
    [0xFF, 0xD8, 0xFF, ..] => "jpeg",
    [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "png",
    [(byte)'G', (byte)'I', (byte)'F', ..] => "gif",
    [(byte)'B', (byte)'M', ..] => "bmp",
    [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "webp",
    [0x49, 0x49, 0x2A, 0x00, ..] or [0x4D, 0x4D, 0x00, 0x2A, ..] => "tiff",
    _ => null,
  };

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
