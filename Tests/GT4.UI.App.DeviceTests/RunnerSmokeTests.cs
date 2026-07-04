using Xunit;

namespace GT4.UI.DeviceTests;

public class RunnerSmokeTests
{
  [Fact]
  public void Runner_executes_tests()
  {
    Assert.True(true);
  }

  [Fact]
  public async Task App_styles_load_into_runner_application()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var found = Application.Current!.Resources.TryGetValue("PageSubtitle", out var style);

    Assert.True(found);
    Assert.IsType<Style>(style);
  }
}
