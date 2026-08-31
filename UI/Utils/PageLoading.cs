using GT4.UI.Abstraction;
using System.ComponentModel;

namespace GT4.UI;

/// <summary>
/// The "a load is in flight and the page has nothing to show yet" flag behind PageLayout's activity
/// indicator. A page owns one and binds the layout to it.
/// </summary>
public sealed class PageLoading : INotifyPropertyChanged
{
  private readonly IAlertService _AlertService;
  private bool _IsLoading;

  public PageLoading(IAlertService alertService)
  {
    _AlertService = alertService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public bool IsLoading
  {
    get => _IsLoading;
    private set
    {
      if (_IsLoading != value)
      {
        _IsLoading = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
      }
    }
  }

  /// <summary>
  /// <paramref name="hasContent"/> is a snapshot, not a live check: a load's apply step clears its
  /// collection before refilling it, and a recomputed check would see that gap and flash the
  /// indicator on the very refresh it must skip.
  /// </summary>
  public void Begin(bool hasContent) => IsLoading = !hasContent;

  public void End() => IsLoading = false;

  public void Run(bool hasContent, Func<Task> work)
  {
    Begin(hasContent);

    _ = SafeTask.Run(async () =>
    {
      try
      {
        await work();
      }
      finally
      {
        await SafeTask.RunOnMainThread(End, _AlertService);
      }
    }, _AlertService);
  }
}
