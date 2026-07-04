using Xunit;

namespace GT4.UI.DeviceTests;

public class TempFailingTest
{
  [Fact]
  public void This_should_fail()
  {
    Assert.True(false);
  }
}
