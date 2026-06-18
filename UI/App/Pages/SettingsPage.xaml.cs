using GT4.Core.Utils;
using GT4.UI.Utils.Settings;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly IEnumerable<ISettingEditor> _SettingEditors;
  private readonly FontScale _FontScale;

  public SettingsPage(IServiceProvider serviceProvider, FontScale fontScale)
  {
    _SettingEditors = serviceProvider.GetKeyedServices<ISettingEditor>(KeyedService.AnyKey);
    _FontScale = fontScale;

    InitializeComponent();
  }

  public IEnumerable<ISettingEditor> SettingEditors => _SettingEditors
    .GroupBy(e => e.Group)
    .SelectMany(g => g.OrderBy(e => e.DisplayName));

  public double FontScaleMinimum => FontScaleSetting.MinFactor;

  public double FontScaleMaximum => FontScaleSetting.MaxFactor;

  public double FontScaleIncrement => FontScaleSetting.StepFactor;

  public double FontScaleFactor
  {
    get => _FontScale.Factor;
    set
    {
      // The Stepper rounds to its increment, so an exact-equality guard is enough to avoid a
      // redundant persist/apply cycle when the binding echoes the current value back.
      if (_FontScale.Factor == value)
        return;

      _FontScale.SetFactor(value);
      OnPropertyChanged();
      OnPropertyChanged(nameof(FontScalePercent));
    }
  }

  public string FontScalePercent => $"{_FontScale.Factor * 100:0}%";

  public ICommand ResetFontScaleCommand => new SafeCommand(() =>
  {
    _FontScale.ResetToDefault();
    OnPropertyChanged(nameof(FontScaleFactor));
    OnPropertyChanged(nameof(FontScalePercent));
  });
}
