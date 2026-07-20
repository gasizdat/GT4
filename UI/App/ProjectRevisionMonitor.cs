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
    _Timer.Start();
  }

  public event EventHandler RevisionChanged
  {
    add => _EventManager.AddEventHandler(value);
    remove => _EventManager.RemoveEventHandler(value);
  }

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
