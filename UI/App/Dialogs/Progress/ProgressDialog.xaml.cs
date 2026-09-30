using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

// Modal shown while a long operation (a GEDCOM import, an HTML export) runs on a background thread. It
// surfaces progress and lets the user cancel: the Cancel button (or the Android hardware back button) trips
// the token the operation observes.
public partial class ProgressDialog : ContentPage
{
  private readonly CancellationTokenSource _Cancellation = new();
  private readonly ICommand _DialogCommand;
  private readonly string _InProgressText;
  private bool _Cancelling;

  public ProgressDialog(string heading, string subject, string inProgressText, IAlertService alertService)
  {
    Heading = heading;
    Subject = subject;
    _InProgressText = inProgressText;
    _DialogCommand = new SafeCommand(Cancel, alertService);
    InitializeComponent();
  }

  public CancellationToken Token => _Cancellation.Token;

  public string Heading { get; init; }

  public string Subject { get; init; }

  public bool CanCancel => !_Cancelling;

  public string StatusText => _Cancelling
    ? UIStrings.HintProgressCancelling
    : _InProgressText;

  public ICommand DialogCommand => _DialogCommand;

  // The hardware back button would otherwise dismiss the modal and leave the operation running headless;
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
