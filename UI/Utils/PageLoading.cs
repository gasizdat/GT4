using GT4.UI.Abstraction;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GT4.UI;

/// <summary>
/// The two "a load is in flight" flags a page hands to PageLayout: <see cref="IsLoading"/> drives the
/// activity indicator, <see cref="IsBusy"/> the wait cursor. A page owns one and binds the layout to it.
/// </summary>
public sealed class PageLoading : INotifyPropertyChanged
{
  private readonly IAlertService _AlertService;
  private bool _IsLoading;
  private bool _IsBusy;

  public PageLoading(IAlertService alertService)
  {
    _AlertService = alertService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  /// <summary>True only while the page has nothing to show: an indicator over content would hide it.</summary>
  public bool IsLoading
  {
    get => _IsLoading;
    set => Set(ref _IsLoading, value);
  }

  /// <summary>True for the whole load, a refresh over existing content included.</summary>
  public bool IsBusy
  {
    get => _IsBusy;
    set => Set(ref _IsBusy, value);
  }

  /// <summary>
  /// <paramref name="hasContent"/> is a snapshot, not a live check: a load's apply step clears its
  /// collection before refilling it, and a recomputed check would see that gap and flash the
  /// indicator on the very refresh it must skip.
  /// </summary>
  public void Begin(bool hasContent)
  {
    IsLoading = !hasContent;
    IsBusy = true;
  }

  public void End()
  {
    IsLoading = false;
    IsBusy = false;
  }

  /// <summary>Runs <paramref name="work"/> in the background between <see cref="Begin"/> and <see cref="End"/>.</summary>
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

  private void Set(ref bool field, bool value, [CallerMemberName] string propertyName = "")
  {
    if (field != value)
    {
      field = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
