using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class EditCaptionDialog : ContentPage
{
  private readonly ICommand _DialogCommand;
  private readonly ICommand _CancelCommand;
  private readonly TaskCompletionSource<string?> _Info = new(null);
  private string? _Caption;
  private bool _IsModified;

  public EditCaptionDialog(string? caption, IAlertService alertService)
  {
    _Caption = caption;
    _DialogCommand = new SafeCommand(OnDialogCommand, alertService);
    _CancelCommand = new SafeCommand(Cancel, alertService);
    InitializeComponent();
  }

  public string? Caption
  {
    get => _Caption;
    set
    {
      if (_Caption != value)
      {
        _Caption = value;
        _IsModified = true;

        OnPropertyChanged(nameof(Caption));
        OnPropertyChanged(nameof(DialogButtonName));
      }
    }
  }

  public Task<string?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  public ICommand CancelCommand => _CancelCommand;

  public string DialogButtonName => _IsModified ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  protected override bool OnBackButtonPressed()
  {
    Cancel();
    return true;
  }

  private void OnDialogCommand() => _Info.TrySetResult(_IsModified ? _Caption : null);

  private void Cancel() => _Info.TrySetResult(null);
}
