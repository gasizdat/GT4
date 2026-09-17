using System.ComponentModel;

namespace GT4.UI.Utils.Settings;

// Drives PageLayout's mutation-menu hiding. A DI singleton (registered by AddUIUtils) so
// PageLayout's binding and ReadOnlyModeSetting always observe the same live instance; App applies
// the persisted value at startup, exactly like BackgroundAnimation.
public sealed class ReadOnlyMode : INotifyPropertyChanged
{
  private bool _IsEnabled;

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanEdit)));
      }
    }
  }

  public bool CanEdit => !IsEnabled;

  public void Apply(string? enabledValue) => IsEnabled = bool.TryParse(enabledValue, out var parsed) && parsed;
}
