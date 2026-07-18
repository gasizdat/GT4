using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Text;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

// Shown when a GEDCOM import declares an ambiguous charset (e.g. "ANSI") that cannot be resolved to a single
// codepage from the file alone. The curated list below covers the codepages GEDCOM's legacy "ANSI" charset
// has commonly meant in practice; anything outside it is a case for a full ANSEL decoder, not this dialog.
public partial class SelectEncodingDialog : ContentPage
{
  private static readonly (string Name, int CodePage)[] Codepages =
  [
    ("Windows-1252 (Western European)", 1252),
    ("Windows-1251 (Cyrillic)", 1251),
    ("Windows-1250 (Central European)", 1250),
    ("Windows-1253 (Greek)", 1253),
    ("Windows-1254 (Turkish)", 1254),
    ("Windows-1257 (Baltic)", 1257),
    ("ISO-8859-1 (Latin-1)", 28591),
    ("KOI8-R (Russian)", 20866),
  ];

  private readonly string _DeclaredCharset;
  private readonly ICommand _DialogCommand;
  private readonly TaskCompletionSource<Encoding?> _Info = new(null);
  private string? _SelectedCodepageName;

  public SelectEncodingDialog(string declaredCharset, IAlertService alertService)
  {
    _DeclaredCharset = declaredCharset;
    _DialogCommand = new SafeCommand(OnSelectEncoding, alertService);
    InitializeComponent();
  }

  public string DeclaredCharsetHint => string.Format(UIStrings.HintGedcomDeclaredCharset_1, _DeclaredCharset);

  public string[] CodepageNames => Codepages.Select(c => c.Name).ToArray();

  public string? SelectedCodepageName
  {
    get => _SelectedCodepageName;
    set
    {
      _SelectedCodepageName = value;
      OnPropertyChanged(nameof(SelectedCodepageName));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public string DialogButtonName => _SelectedCodepageName is not null ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<Encoding?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  private void OnSelectEncoding()
  {
    var codePage = Codepages.FirstOrDefault(c => c.Name == _SelectedCodepageName).CodePage;
    _Info.SetResult(_SelectedCodepageName is null ? null : Encoding.GetEncoding(codePage));
  }
}
