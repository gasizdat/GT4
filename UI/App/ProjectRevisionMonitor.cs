using GT4.Core.Project.Abstraction;
using GT4.UI.Abstraction;

namespace GT4.UI;

// Polls ProjectRevision once a second so an already-open page catches a commit made elsewhere --
// OnNavigatedTo alone only catches changes made before the page opened. WeakEventManager-backed: pages
// are effectively transient (new instance per Shell navigation) but this monitor is an app-lifetime
// singleton, so a plain event subscription would leak one page per navigation.
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

  // Runs only while at least one page is listening, so an idle app doesn't tick forever. A
  // WeakEventManager subscriber can be collected without ever calling remove, so this count can only
  // overshoot the true number of live handlers -- never masks a real change.
  public event EventHandler RevisionChanged
  {
    add
    {
      _EventManager.AddEventHandler(value);
      if (Interlocked.Increment(ref _SubscriberCount) == 1)
      {
        // Seed the baseline so a subscriber's first tick doesn't see a spurious change.
        PrimeLastRevision();
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

  // A page's Loaded event can fire after its native view already exists -- tests must wait for this
  // before calling CheckRevision, or the check can land before anyone is listening.
  internal int SubscriberCount => _SubscriberCount;

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

  private void PrimeLastRevision()
  {
    try
    {
      _LastRevision = _CurrentProjectProvider.HasCurrentProject ? _CurrentProjectProvider.Project.ProjectRevision : null;
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      _LastRevision = null;
    }
  }
}
