using GT4.UI.Abstraction;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class EditCaptionDialog : ContentPage
{
  private readonly string? _OriginalCaption;
  private readonly ICommand _DialogCommand;
  private readonly ICommand _CancelCommand;
  private readonly TaskCompletionSource<string?> _Info = new();
  private string? _Caption;

  public EditCaptionDialog(string? caption, IAlertService alertService)
  {
    _OriginalCaption = caption;
    _Caption = caption;
    _DialogCommand = new SafeCommand(Accept, alertService);
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
        OnPropertyChanged(nameof(Caption));
      }
    }
  }

  public Task<string?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  public ICommand CancelCommand => _CancelCommand;

  protected override bool OnBackButtonPressed()
  {
    Cancel();
    return true;
  }

  private void Accept() => _Info.TrySetResult(_Caption);

  private void Cancel() => _Info.TrySetResult(_OriginalCaption);
}
