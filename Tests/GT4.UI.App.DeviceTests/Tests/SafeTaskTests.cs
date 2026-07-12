using GT4.Core.Project.Abstraction;
using GT4.UI;
using Xunit;

namespace GT4.UI.DeviceTests;

public sealed class SafeTaskTests
{
  [Fact]
  public void IsProjectTeardown_ObjectDisposedException_ReturnsTrue() =>
    Assert.True(SafeTask.IsProjectTeardown(new ObjectDisposedException(nameof(SafeTaskTests))));

  [Fact]
  public void IsProjectTeardown_ProjectNotOpenedException_ReturnsTrue() =>
    Assert.True(SafeTask.IsProjectTeardown(new ProjectNotOpenedException()));

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
}
