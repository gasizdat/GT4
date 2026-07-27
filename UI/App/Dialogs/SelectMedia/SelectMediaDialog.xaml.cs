using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectMediaDialog : ContentPage
{
  public record class Factory(IAlertService AlertService)
  {
    public SelectMediaDialog Create(MediaLinkItem[] items) => new SelectMediaDialog(items, AlertService);
  }

  private readonly MediaLinkItem[] _Items;
  private readonly TaskCompletionSource<MediaLinkItem?> _Info = new(null);
  private readonly ICommand _DialogCommand;
  private MediaLinkItem? _SelectedItem;

  public SelectMediaDialog(MediaLinkItem[] items, IAlertService alertService)
  {
    _Items = items;
    _DialogCommand = new SafeCommand(OnDialogCommand, alertService);

    InitializeComponent();
  }

  public IEnumerable<MediaLinkItem> Items => _Items;

  public MediaLinkItem? SelectedItem
  {
    get => _SelectedItem;
    set
    {
      _SelectedItem = value;
      OnPropertyChanged(nameof(SelectedItem));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public string DialogButtonName => _SelectedItem is not null ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<MediaLinkItem?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  protected Task OnDialogCommand(object obj)
  {
    if (obj is string commandName && commandName == "SelectMediaCommand")
    {
      _Info.SetResult(_SelectedItem);
    }

    return Task.CompletedTask;
  }
}

public sealed record MediaLinkItem(int Id, bool IsInlineImage, string DisplayName);
