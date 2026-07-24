using GT4.UI.Abstraction;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers ProjectRevisionMonitor's CheckRevision (the timer's tick handler, invoked directly here
/// instead of waiting on the real 1s timer) against a mocked ICurrentProjectProvider.
/// </summary>
public sealed class ProjectRevisionMonitorTests
{
  private static async Task<ProjectRevisionMonitor> CreateMonitorAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(
      () => (ProjectRevisionMonitor)services.Provider.GetRequiredService<IProjectRevisionMonitor>());
  }

  [Fact]
  public async Task CheckRevision_DoesNotFire_OnTheFirstCheckAfterSubscribing_WhenTheRevisionIsUnchanged()
  {
    var services = new TestServices();
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;

    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(0, fireCount);
  }

  [Fact]
  public async Task CheckRevision_DoesNotFireAgain_WhenTheRevisionIsUnchanged()
  {
    var services = new TestServices();
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;
    services.Project.SetupGet(p => p.ProjectRevision).Returns(99L);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(1, fireCount);
  }

  [Fact]
  public async Task CheckRevision_FiresAgain_WhenTheRevisionChanges()
  {
    var services = new TestServices();
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;
    services.Project.SetupGet(p => p.ProjectRevision).Returns(99L);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    services.Project.SetupGet(p => p.ProjectRevision).Returns(100L);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(2, fireCount);
  }

  [Fact]
  public async Task CheckRevision_DoesNotFire_WhileNoProjectIsOpen()
  {
    var services = new TestServices();
    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(false);
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;

    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(0, fireCount);
  }

  // #153: app background/foreground closes then reopens the project. Because ProjectRevision is a
  // persisted counter, an untouched reopen reports the same value, so the monitor must keep its
  // baseline across the no-project window and stay quiet -- the pre-fix code nulled the baseline and
  // false-fired here, forcing a full page reload on every resume.
  [Fact]
  public async Task CheckRevision_DoesNotFire_WhenTheProjectReopensUnchanged()
  {
    var services = new TestServices();
    services.Project.SetupGet(p => p.ProjectRevision).Returns(5L);
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;

    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(false);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(true);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(0, fireCount);
  }

  // The flip side of preserving the baseline: a genuine change committed while the project was closed
  // still advances the persisted counter, so the first tick after reopen must fire. (The monitor-only
  // rebaseline this replaced would have suppressed this.)
  [Fact]
  public async Task CheckRevision_Fires_WhenTheProjectChangedWhileClosed()
  {
    var services = new TestServices();
    services.Project.SetupGet(p => p.ProjectRevision).Returns(5L);
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;

    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(false);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    services.Project.SetupGet(p => p.ProjectRevision).Returns(6L);
    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(true);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(1, fireCount);
  }
}
