using GT4.UI.Components;

namespace GT4.UI.DeviceTests;

internal sealed class TestableRelativeRowView : RelativeRowView
{
  public RelativeConnectorsDrawable ConnectorsDrawableForTest => ConnectorsDrawable;

  public RelativeInfoView InfoViewForTest => InfoView;

  public Thickness ContentMarginForTest => ContentMargin;
}
