using GT4.UI.Components;

namespace GT4.UI.DeviceTests;

internal sealed class TestableSettingEditorView : SettingEditorView
{
  public TestableSettingEditorView(IServiceProvider serviceProvider) : base(serviceProvider)
  {
  }
}
