using GT4.Core.Project.Abstraction;
using GT4.UI.Abstraction;
using Moq;
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
    services.Project.SetupGet(p => p.ProjectRevision).Returns(99);
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
    services.Project.SetupGet(p => p.ProjectRevision).Returns(99);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    services.Project.SetupGet(p => p.ProjectRevision).Returns(100);
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

  // App background/foreground closes then reopens the same project. Even though the reopened document
  // reseeds ProjectRevision to a fresh value, the monitor must treat it as a new baseline and stay
  // quiet -- the pre-fix behavior fired here and forced a full page reload on every resume (#153).
  [Fact]
  public async Task CheckRevision_DoesNotFire_WhenTheProjectReopensAfterBeingClosed()
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

    Assert.Equal(0, fireCount);
  }

  // A fast reopen can leave no close-window tick to reset the baseline: the monitor sees a brand-new
  // ProjectDocument instance while HasCurrentProject never observably went false. Keying rebaseline on
  // the instance -- not on a null baseline -- keeps it quiet even then.
  [Fact]
  public async Task CheckRevision_DoesNotFire_WhenTheProjectInstanceIsSwappedWithoutAClosedTick()
  {
    var services = new TestServices();
    services.Project.SetupGet(p => p.ProjectRevision).Returns(99);
    var monitor = await CreateMonitorAsync(services);
    var fireCount = 0;
    monitor.RevisionChanged += (_, _) => fireCount++;

    // A fresh document whose reseeded revision differs from the old baseline -- a revision-only
    // compare would fire on 99 != 7.
    var reopened = new Mock<IProjectDocument>();
    reopened.SetupGet(p => p.ProjectRevision).Returns(7);
    services.CurrentProjectProvider.SetupGet(p => p.Project).Returns(reopened.Object);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);

    Assert.Equal(0, fireCount);
  }
}
