using GT4.Core.Gedcom;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;

namespace GT4.UI;

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
  /// Resolves the encoding <paramref name="file"/>'s declared <c>CHAR</c> header calls for, prompting the
  /// user for a codepage when that value is ambiguous (e.g. <c>ANSI</c>), and returns a reader over its
  /// content. Returns null if the user cancelled the codepage prompt. FileResult.OpenReadAsync() opens a
  /// fresh stream from the start of the file on every call (on both Windows and Android it is backed by a
  /// re-openable file path, never a single-use stream), so detection reads only the header from one open and
  /// decoding gets a second, independent one -- the whole file is never buffered into memory just to read a
  /// handful of header lines.
  /// </summary>
  public Task<TextReader?> ResolveReaderAsync(FileResult file, INavigation navigation) =>
    ResolveReaderAsync(file.OpenReadAsync, navigation);

  // Split out from the FileResult overload so detection, the codepage prompt and decoding are testable
  // without FileResult: its OpenReadAsync isn't virtual and its path-based constructor doesn't produce a
  // working instance on Windows (it reads an internal StorageFile the constructor never populates), so there
  // is no way to feed it an in-memory stream from a test.
  internal async Task<TextReader?> ResolveReaderAsync(Func<Task<Stream>> openStreamAsync, INavigation navigation)
  {
    GedcomCharsetResult charset;
    using (var headerStream = await openStreamAsync())
    {
      charset = await GedcomCharset.DetectAsync(headerStream);
    }

    var encoding = charset.Encoding;
    if (charset.NeedsCodepage)
    {
      var dialog = new SelectEncodingDialog(charset.DeclaredValue!, _AlertService);
      await navigation.PushModalAsync(dialog);
      encoding = await dialog.Info;
      await navigation.PopModalAsync();
    }

    if (encoding is null)
      return null;

    // detectEncodingFromByteOrderMarks stays on its default (true): when the file actually starts with a
    // BOM, StreamReader strips it and honors it, matching the BOM-wins precedence GedcomCharset.DetectAsync
    // already applied above; when there is none (the ANSI/codepage case), it has no effect and encoding is
    // used as-is.
    return new StreamReader(await openStreamAsync(), encoding);
  }
}
