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
  /// content, or null if the user cancelled the codepage prompt.
  /// </summary>
  public Task<TextReader?> ResolveReaderAsync(FileResult file, INavigation navigation) =>
    ResolveReaderAsync(file.OpenReadAsync, navigation);

  // Seam for testing without FileResult, whose OpenReadAsync isn't virtual and whose path-based
  // constructor doesn't produce a working instance on Windows.
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

    return new StreamReader(await openStreamAsync(), encoding);
  }
}
