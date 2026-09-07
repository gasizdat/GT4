using GT4.Core.Project.Abstraction;
using GT4.UI.Abstraction;
using Moq;
using Xunit;

namespace GT4.UI.Utils.Tests;

public sealed class SafeTaskTests
{
  [Fact]
  public void IsProjectTeardown_ObjectDisposedException_ReturnsTrue() =>
    Assert.True(SafeTask.IsProjectTeardown(new ObjectDisposedException(nameof(SafeTaskTests))));

  [Fact]
  public void IsProjectTeardown_ProjectNotOpenedException_ReturnsTrue() =>
    Assert.True(SafeTask.IsProjectTeardown(new ProjectNotOpenedException()));

  [Fact]
  public void IsProjectTeardown_OperationCanceledException_ReturnsTrue() =>
    Assert.True(SafeTask.IsProjectTeardown(new OperationCanceledException()));

  // What an awaited cancelled task actually throws, so it has to qualify as well.
  [Fact]
  public void IsProjectTeardown_TaskCanceledException_ReturnsTrue() =>
    Assert.True(SafeTask.IsProjectTeardown(new TaskCanceledException()));

  [Fact]
  public void IsProjectTeardown_UnrelatedException_ReturnsFalse() =>
    Assert.False(SafeTask.IsProjectTeardown(new InvalidOperationException()));

  [Fact]
  public void IsProjectTeardown_AggregateOfOnlyTeardownExceptions_ReturnsTrue()
  {
    var aggregate = new AggregateException(new ObjectDisposedException(""), new ProjectNotOpenedException());

    Assert.True(SafeTask.IsProjectTeardown(aggregate));
  }

  [Fact]
  public void IsProjectTeardown_AggregateWithOneGenuineFailure_ReturnsFalse()
  {
    // A real failure aggregated alongside a benign one must still surface, not be swallowed.
    var aggregate = new AggregateException(new ObjectDisposedException(""), new InvalidOperationException());

    Assert.False(SafeTask.IsProjectTeardown(aggregate));
  }

  [Fact]
  public void IsProjectTeardown_EmptyAggregate_ReturnsFalse() =>
    Assert.False(SafeTask.IsProjectTeardown(new AggregateException()));

  // An alert raised from background work is shown by pushing a modal onto the UI thread, which is
  // fatal to WinUI when it lands while a CollectionView is realizing items (issue #370). A cancelled
  // operation must therefore stay silent.
  [Fact]
  public async Task GuardAsync_CancelledWork_ShowsNoAlert()
  {
    var alertService = new Mock<IAlertService>();
    alertService.Setup(a => a.ShowErrorAsync(It.IsAny<Exception>())).Returns(Task.CompletedTask);

    await SafeTask.GuardAsync(() => throw new OperationCanceledException(), alertService.Object);

    alertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task GuardAsync_GenuineFailure_ShowsAlert()
  {
    var alertService = new Mock<IAlertService>();
    alertService.Setup(a => a.ShowErrorAsync(It.IsAny<Exception>())).Returns(Task.CompletedTask);
    var failure = new InvalidOperationException();

    await SafeTask.GuardAsync(() => throw failure, alertService.Object);

    alertService.Verify(a => a.ShowErrorAsync(failure), Times.Once());
  }
}
