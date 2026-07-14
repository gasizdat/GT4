using GT4.UI.Abstraction;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Shared body for the "wait for a fire-and-forget page load to settle" helper duplicated across
/// FamilyTreePageTests and PersonPageTests: snapshot the load-completion counter, run the triggering
/// interaction, then poll for either a load completion or an IAlertService invocation -- rethrowing
/// the real exception if it was the latter, instead of leaving the caller to chase a bare timeout.
/// </summary>
internal static class LoadWait
{
  public static async Task UntilAsync(Func<int> completedLoads, TestServices services, Action interact, string label)
  {
    var loadsBefore = 0;
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      loadsBefore = completedLoads();
      interact();
    });

    await Poll.UntilAsync(
      () => Task.FromResult(completedLoads() > loadsBefore || services.AlertService.Invocations.Count > 0),
      ready => ready,
      timeoutMessage: $"{label} load neither completed nor reported an error.");

    var errorInvocation = services.AlertService.Invocations.FirstOrDefault(i => i.Method.Name == nameof(IAlertService.ShowErrorAsync));
    if (errorInvocation is not null && completedLoads() == loadsBefore)
    {
      throw new Exception($"{label} load failed", (Exception)errorInvocation.Arguments[0]);
    }
  }
}
