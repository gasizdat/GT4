using GT4.Core.Gedcom;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using System.Text;

namespace GT4.UI.Pages;

// Shared by ProjectListPage and ProjectPage: both let the user pick a GEDCOM file to import and need the
// same declared-charset detection and (when ambiguous) codepage prompt before the file can be decoded.
public sealed class GedcomImportEncoding
{
  private readonly IAlertService _AlertService;

  public GedcomImportEncoding(IAlertService alertService)
  {
    _AlertService = alertService;
  }

  /// <summary>
  /// Reads <paramref name="file"/> once, resolves the encoding its declared <c>CHAR</c> header calls for,
  /// prompting the user for a codepage when that value is ambiguous (e.g. <c>ANSI</c>), and returns a reader
  /// over its content. Returns null if the user cancelled the codepage prompt.
  /// </summary>
  public async Task<TextReader?> ResolveReaderAsync(FileResult file, INavigation navigation)
  {
    byte[] bytes;
    using (var stream = await file.OpenReadAsync())
    using (var buffer = new MemoryStream())
    {
      await stream.CopyToAsync(buffer);
      bytes = buffer.ToArray();
    }

    return await ResolveReaderFromBytesAsync(bytes, navigation);
  }

  // Split out from ResolveReaderAsync so detection, the codepage prompt and decoding are testable without
  // FileResult: its OpenReadAsync isn't virtual and its path-based constructor doesn't produce a working
  // instance on Windows (it reads an internal StorageFile the constructor never populates), so there is no
  // way to feed it in-memory bytes from a test.
  internal async Task<TextReader?> ResolveReaderFromBytesAsync(byte[] bytes, INavigation navigation)
  {
    var charset = GedcomCharset.Detect(bytes);
    var encoding = charset.Encoding;
    if (charset.NeedsCodepage)
    {
      var dialog = new SelectEncodingDialog(charset.DeclaredValue!, _AlertService);
      await navigation.PushModalAsync(dialog);
      encoding = await dialog.Info;
      await navigation.PopModalAsync();
    }

    // detectEncodingFromByteOrderMarks stays on its default (true): when bytes actually start with a BOM,
    // StreamReader strips it and honors it, matching the BOM-wins precedence GedcomCharset.Detect already
    // applied above; when there is none (the ANSI/codepage case), it has no effect and encoding is used as-is.
    return encoding is null ? null : new StreamReader(new MemoryStream(bytes), encoding);
  }
}
