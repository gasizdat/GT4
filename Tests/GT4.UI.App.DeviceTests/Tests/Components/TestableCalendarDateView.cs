using GT4.UI.Components;

namespace GT4.UI.DeviceTests;

internal sealed class TestableCalendarDateView : CalendarDateView
{
  public TestableCalendarDateView(IServiceProvider serviceProvider) : base(serviceProvider)
  {
  }
}
