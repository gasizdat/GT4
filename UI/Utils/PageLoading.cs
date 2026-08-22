using GT4.UI.Abstraction;
using System.ComponentModel;

namespace GT4.UI;

/// <summary>
/// The "a load is in flight and the page has nothing to show yet" flag behind PageLayout's activity
/// indicator. A page owns one and binds the layout to it, the way BackgroundAnimation drives that
/// same layout's background.
/// </summary>
public sealed class PageLoading : INotifyPropertyChanged
{
  private bool _IsLoading;

  public event PropertyChangedEventHandler? PropertyChanged;

  public bool IsLoading
  {
    get => _IsLoading;
    set
    {
      if (_IsLoading != value)
      {
        _IsLoading = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
      }
    }
  }

  /// <summary>
  /// Holds <see cref="IsLoading"/> for the duration of <paramref name="work"/>, unless the page
  /// already has content on screen. <paramref name="hasContent"/> is read once, here, because the
  /// load's apply step clears its collection before refilling it -- a recomputed check would see
  /// that gap and flash the indicator on the very refresh it must skip. Clearing happens in a
  /// finally, so a failed or silently-swallowed load cannot leave the indicator spinning.
  /// </summary>
  public void Run(bool hasContent, Func<Task> work, IAlertService alertService)
  {
    IsLoading = !hasContent;

    _ = SafeTask.Run(async () =>
    {
      try
      {
        await work();
      }
      finally
      {
        await SafeTask.RunOnMainThread(() => IsLoading = false, alertService);
      }
    }, alertService);
  }
}
