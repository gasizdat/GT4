using System.ComponentModel;

namespace GT4.UI.Utils.Settings;

// Drives PageLayout's animated background GIF. A DI singleton (registered by AddUIUtils) so
// PageLayout's binding and BackgroundAnimationSetting always observe the same live instance;
// App applies the persisted value at startup, exactly like FontScale.
public sealed class BackgroundAnimation : INotifyPropertyChanged
{
  // Set by GT4.UI.App.DeviceTests before MauiApp.CreateBuilder() runs -- before any DI container,
  // and so any BackgroundAnimation instance, exists. Rapid page churn during test navigation races
  // Android's native GIF decoder against page teardown, crashing the process with a native "Pure
  // virtual function called" abort (not catchable in managed code), so tests always disable it.
  public static bool InitialIsEnabled { get; set; } = true;

  private bool _IsEnabled = InitialIsEnabled;

  public event PropertyChangedEventHandler? PropertyChanged;

  public bool IsEnabled
  {
    get => _IsEnabled;
    private set
    {
      if (_IsEnabled != value)
      {
        _IsEnabled = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
      }
    }
  }

  public void Apply(string? enabledValue) => IsEnabled = !bool.TryParse(enabledValue, out var parsed) || parsed;
}
