using GT4.Core.Utils;
using GT4.UI.Utils.Settings;

namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly IEnumerable<ISettingEditor> _SettingEditors;

  public SettingsPage(SettingEditorsResolver settingEditorsResolver)
  {
    _SettingEditors = settingEditorsResolver();

    InitializeComponent();
  }

  public IEnumerable<ISettingEditor> SettingEditors => _SettingEditors
    .GroupBy(e => e.Group)
    .SelectMany(g => g.OrderBy(e => e.DisplayName));
}
