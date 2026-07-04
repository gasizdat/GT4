using GT4.Core.Project.Abstraction;

namespace GT4.UI;

/// <summary>
/// Helpers for fire-and-forget background work that may touch the project document. The app lifecycle
/// can close/dispose the document underneath such work — most visibly when Android backgrounds the
/// app and <see cref="App"/> closes the current project — which surfaces as
/// <see cref="ObjectDisposedException"/> or <see cref="ProjectNotOpenedException"/>. Those are
/// expected during teardown and swallowed quietly; any other failure is surfaced through the
/// caller's <see cref="IAlertService"/>. Without this, an exception escaping a Task.Run or a
/// posted continuation is unobserved and terminates the app.
/// </summary>
internal static class SafeTask
{
  /// <summary>Runs <paramref name="work"/> on a background thread, guarding against the teardown race.</summary>
  public static Task Run(Func<Task> work, IAlertService alertService) =>
    Task.Run(() => GuardAsync(work, alertService));

  /// <summary>Runs <paramref name="work"/> on the UI thread, guarding against the teardown race.</summary>
  public static Task RunOnMainThread(Action work, IAlertService alertService) =>
    MainThread.InvokeOnMainThreadAsync(() => Guard(work, alertService));

  /// <summary>
  /// True when the exception is the benign "the project was closed underneath us" race rather than a
  /// genuine failure. Use it in <c>catch ... when</c> clauses to swallow teardown noise quietly.
  /// Recurses into AggregateException because several call sites block on Task.Result, which wraps
  /// the original exception rather than rethrowing it directly. Requires every inner exception to be
  /// teardown-related (not just the first) so a genuine failure aggregated alongside a benign one is
  /// still surfaced instead of silently swallowed.
  /// </summary>
  public static bool IsProjectTeardown(Exception exception) =>
    exception switch
    {
      ObjectDisposedException or ProjectNotOpenedException => true,
      AggregateException aggregate => aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsProjectTeardown),
      _ => false
    };

  private static async Task GuardAsync(Func<Task> work, IAlertService alertService)
  {
    try
    {
      await work();
    }
    catch (Exception ex) when (IsProjectTeardown(ex))
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      await alertService.ShowErrorAsync(ex);
    }
  }

  private static void Guard(Action work, IAlertService alertService)
  {
    try
    {
      work();
    }
    catch (Exception ex) when (IsProjectTeardown(ex))
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      _ = alertService.ShowErrorAsync(ex);
    }
  }
}
