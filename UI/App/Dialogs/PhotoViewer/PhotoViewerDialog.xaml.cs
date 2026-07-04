using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class PhotoViewerDialog : ContentPage
{
  private readonly ImageSource[] _Photos;
  private readonly ICommand _CloseCommand;

  public PhotoViewerDialog(byte[][] photos, IPageAlertService pageAlertService)
  {
    _Photos = (photos ?? [])
      .Where(photo => photo.Length > 0)
      .Select(ImageUtils.ImageFromBytes)
      .ToArray();

    _CloseCommand = new SafeCommand(OnCloseAsync, pageAlertService);

    InitializeComponent();
  }

  public ImageSource[] Photos => _Photos;

  public ICommand CloseCommand => _CloseCommand;

  private Task OnCloseAsync() => Navigation.PopModalAsync();
}
