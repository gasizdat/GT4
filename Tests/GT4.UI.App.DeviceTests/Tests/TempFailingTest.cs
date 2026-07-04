using Xunit;

namespace GT4.UI.DeviceTests;

public class TempFailingTest
{
  [Fact(Skip = "This is a test for the unhappy scenario. Disabled")]
  public void This_should_fail()
  {
    Assert.True(false);
  }
}
