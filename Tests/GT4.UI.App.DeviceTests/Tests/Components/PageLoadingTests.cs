using GT4.UI.Abstraction;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers the flags a page hands to PageLayout's activity indicator and wait cursor. Runs as a device
/// test rather than a plain unit test because Run's completion marshals through MainThread, which needs
/// a live MAUI dispatcher.
/// </summary>
public class PageLoadingTests
{
  [Fact]
  public async Task A_load_that_starts_with_content_on_screen_never_raises_the_flag()
  {
    var alertService = new Mock<IAlertService>();
    var loading = new PageLoading(alertService.Object);
    var release = new TaskCompletionSource();

    loading.Run(hasContent: true, () => release.Task);

    Assert.False(loading.IsLoading);
    release.SetResult();
    await loading.UntilIdleAsync("A load over existing content raised the flag.");
  }

  // The wait cursor follows IsBusy, so it has to stay raised through the very refresh the indicator skips.
  [Fact]
  public async Task A_load_that_starts_with_content_on_screen_still_reports_busy()
  {
    var alertService = new Mock<IAlertService>();
    var loading = new PageLoading(alertService.Object);
    var release = new TaskCompletionSource();

    loading.Run(hasContent: true, () => release.Task);

    Assert.True(loading.IsBusy);

    release.SetResult();
    await loading.UntilIdleAsync("A load over existing content stayed busy.");
    Assert.False(loading.IsBusy);
  }

  [Fact]
  public async Task A_failed_load_still_lowers_the_flag()
  {
    var alertService = new Mock<IAlertService>();
    var loading = new PageLoading(alertService.Object);

    loading.Run(hasContent: false, () => Task.FromException(new InvalidOperationException()));

    Assert.True(loading.IsLoading);
    await loading.UntilIdleAsync("A failed load left the flag raised.");
    alertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Once());
  }

  // SafeTask swallows this race silently, so nothing but the finally would ever lower the flag.
  [Fact]
  public async Task A_load_lost_to_the_project_teardown_race_still_lowers_the_flag()
  {
    var alertService = new Mock<IAlertService>();
    var loading = new PageLoading(alertService.Object);

    loading.Run(hasContent: false, () => Task.FromException(new ObjectDisposedException("project")));

    await loading.UntilIdleAsync("A load lost to project teardown left the flag raised.");
    alertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }
}
