using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class PhotoViewerDialog : ContentPage
{
  private readonly ImageSource[] _Photos;
  private readonly ICommand _CloseCommand;

  public PhotoViewerDialog(ImageSource[] photos, IAlertService alertService)
  {
    _Photos = photos ?? [];

    _CloseCommand = new SafeCommand(OnCloseAsync, alertService);

    InitializeComponent();
  }

  public ImageSource[] Photos => _Photos;

  public ICommand CloseCommand => _CloseCommand;

  private Task OnCloseAsync() => Navigation.PopModalAsync();
}
