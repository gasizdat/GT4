using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

// Modal shown while a GEDCOM import runs on a background thread. It surfaces progress and lets the user
// cancel: the Cancel button (or the Android hardware back button) trips the token the import observes.
public partial class GedcomImportDialog : ContentPage
{
  private readonly CancellationTokenSource _Cancellation = new();
  private readonly ICommand _DialogCommand;
  private bool _Cancelling;

  public GedcomImportDialog(string importingProjectName, IAlertService alertService)
  {
    ImportingProjectName = importingProjectName;
    _DialogCommand = new SafeCommand(Cancel, alertService);
    InitializeComponent();
  }

  public CancellationToken Token => _Cancellation.Token;

  public string ImportingProjectName { get; init; }

  public bool CanCancel => !_Cancelling;

  public string StatusText => _Cancelling
    ? UIStrings.HintGedcomImportCancelling
    : UIStrings.HintGedcomImportInProgress;

  public ICommand DialogCommand => _DialogCommand;

  // The hardware back button would otherwise dismiss the modal and leave the import running headless;
  // route it to cancellation and swallow the dismissal.
  protected override bool OnBackButtonPressed()
  {
    Cancel();
    return true;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    _Cancellation.Dispose();
  }

  private void Cancel()
  {
    if (_Cancelling)
      return;

    _Cancelling = true;
    _Cancellation.Cancel(throwOnFirstException: true);
    OnPropertyChanged(nameof(CanCancel));
    OnPropertyChanged(nameof(StatusText));
  }
}
