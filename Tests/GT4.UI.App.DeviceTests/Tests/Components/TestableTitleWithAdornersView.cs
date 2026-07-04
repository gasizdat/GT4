using GT4.UI.Components;

namespace GT4.UI.DeviceTests;

internal sealed class TestableTitleWithAdornersView : TitleWithAdornersView
{
  public void InvokeEditAdornerClicked() => OnEditAdornerClicked(this, EventArgs.Empty);

  public void InvokeDeleteAdornerClicked() => OnDeleteAdornerClicked(this, EventArgs.Empty);
}
