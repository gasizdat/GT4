using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Text;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

// Shown when a GEDCOM import declares an ambiguous charset (e.g. "ANSI") that cannot be resolved to a single
// codepage from the file alone; ANSEL is a case for a full ANSEL decoder, not this dialog.
public partial class SelectEncodingDialog : ContentPage
{
  private static readonly EncodingInfo[] SortedEncodings = Encoding.GetEncodings()
    .OrderBy(enc => enc.DisplayName)
    .ThenBy(enc => enc.Name)
    .ToArray();

  private readonly string _DeclaredCharset;
  private readonly ICommand _DialogCommand;
  private readonly TaskCompletionSource<Encoding?> _Info = new(null);
  private EncodingInfo? _SelectedEncoding;

  public SelectEncodingDialog(string declaredCharset, IAlertService alertService)
  {
    _DeclaredCharset = declaredCharset;
    _DialogCommand = new SafeCommand(OnSelectEncoding, alertService);
    InitializeComponent();
  }

  public string DeclaredCharsetHint => string.Format(UIStrings.HintGedcomDeclaredCharset_1, _DeclaredCharset);

  public EncodingInfo[] Encodings => SortedEncodings;

  public EncodingInfo? SelectedEncoding
  {
    get => _SelectedEncoding;
    set
    {
      _SelectedEncoding = value;
      OnPropertyChanged(nameof(SelectedEncoding));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public string DialogButtonName => _SelectedEncoding is not null ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<Encoding?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  private void OnSelectEncoding()
  {
    _Info.SetResult(_SelectedEncoding?.GetEncoding());
  }
}
