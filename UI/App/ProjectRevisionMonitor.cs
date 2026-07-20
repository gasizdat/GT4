using GT4.Core.Project.Abstraction;
using GT4.UI.Abstraction;

namespace GT4.UI;

// Polls ProjectRevision (an in-memory Interlocked.Read, no I/O) once a second so a page that's already
// open catches a commit made elsewhere -- OnNavigatedTo alone only catches changes made before the page
// was opened. RevisionChanged is WeakEventManager-backed: pages are effectively transient (a new
// instance per Shell navigation) while this monitor is an app-lifetime singleton, so a plain event
// subscription without disciplined unsubscribe would leak one page per navigation.
internal sealed class ProjectRevisionMonitor : IProjectRevisionMonitor
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly WeakEventManager _EventManager = new();
  private readonly IDispatcherTimer _Timer;
  private long? _LastRevision;
  private int _SubscriberCount;

  public ProjectRevisionMonitor(ICurrentProjectProvider currentProjectProvider)
  {
    _CurrentProjectProvider = currentProjectProvider;

    // Application.Current.Dispatcher, not an injected IDispatcher: this is a plain DI singleton built
    // both by the real MAUI host and by TestServices' bare ServiceCollection (which doesn't register
    // IDispatcher), so it has to reach the dispatcher the same way ImagePresenter does.
    _Timer = Application.Current!.Dispatcher.CreateTimer();
    _Timer.Interval = TimeSpan.FromSeconds(1);
    _Timer.IsRepeating = true;
    _Timer.Tick += (_, _) => CheckRevision();
  }

  // Ticking only while at least one page is actually listening keeps an idle app (or, incidentally, a
  // test run that constructs many of these) from accumulating live timers. A WeakEventManager
  // subscriber can be collected without ever calling remove, so this count can only overshoot the true
  // number of live handlers -- worst case the timer keeps running a little longer than strictly
  // necessary, never less, so it never masks a real change.
  public event EventHandler RevisionChanged
  {
    add
    {
      _EventManager.AddEventHandler(value);
      if (Interlocked.Increment(ref _SubscriberCount) == 1)
      {
        _Timer.Start();
      }
    }
    remove
    {
      _EventManager.RemoveEventHandler(value);
      if (Interlocked.Decrement(ref _SubscriberCount) == 0)
      {
        _Timer.Stop();
      }
    }
  }

  // Test seam (InternalsVisibleTo GT4.UI.App.DeviceTests): a page's own subscription only becomes
  // active once its Loaded event has actually fired, which can lag behind its native view merely
  // existing -- a test must wait for this before calling CheckRevision, or the check can land before
  // anyone is listening.
  internal int SubscriberCount => _SubscriberCount;

  // Also the test seam (InternalsVisibleTo GT4.UI.App.DeviceTests) -- tests call this directly instead
  // of waiting on the real timer.
  internal void CheckRevision()
  {
    // HasCurrentProject and Project are two separate locked reads on ICurrentProjectProvider, so a
    // project close landing between them would otherwise throw ProjectNotOpenedException out of this
    // tick handler once a second -- swallow it exactly like the teardown race everywhere else in the
    // app, rather than let an unhandled exception escape a timer callback.
    try
    {
      if (!_CurrentProjectProvider.HasCurrentProject)
      {
        _LastRevision = null;
        return;
      }

      var revision = _CurrentProjectProvider.Project.ProjectRevision;
      if (_LastRevision != revision)
      {
        _LastRevision = revision;
        _EventManager.HandleEvent(this, EventArgs.Empty, nameof(RevisionChanged));
      }
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      _LastRevision = null;
    }
  }
}
