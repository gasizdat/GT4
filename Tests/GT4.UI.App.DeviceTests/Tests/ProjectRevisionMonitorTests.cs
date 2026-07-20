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
  public async Task CheckRevision_FiresOnce_WhenTheRevisionFirstBecomesKnown()
  {
    var services = new TestServices();
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;

    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(1, fireCount);
  }

  [Fact]
  public async Task CheckRevision_DoesNotFireAgain_WhenTheRevisionIsUnchanged()
  {
    var services = new TestServices();
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;
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
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    services.Project.SetupGet(p => p.ProjectRevision).Returns(99);

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

  [Fact]
  public async Task CheckRevision_FiresExactlyOnce_WhenAProjectOpensAfterNoneWasOpen()
  {
    var services = new TestServices();
    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(false);
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    services.CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(true);

    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(1, fireCount);
  }
}
