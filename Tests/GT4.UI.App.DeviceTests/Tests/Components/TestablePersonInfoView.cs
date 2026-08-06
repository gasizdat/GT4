using GT4.UI.Components;

namespace GT4.UI.DeviceTests;

internal sealed class TestablePersonInfoView : PersonInfoView
{
  public TestablePersonInfoView(IServiceProvider serviceProvider) : base(serviceProvider)
  {
  }
}
