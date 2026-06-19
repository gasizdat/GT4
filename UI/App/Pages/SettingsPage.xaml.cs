using GT4.Core.Utils;

namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly IEnumerable<ISettingEditor> _SettingEditors;

  public SettingsPage(IServiceProvider serviceProvider)
  {
    _SettingEditors = serviceProvider.GetKeyedServices<ISettingEditor>(KeyedService.AnyKey);

    InitializeComponent();
  }

  public IEnumerable<ISettingEditor> SettingEditors => _SettingEditors
    .GroupBy(e => e.Group)
    .SelectMany(g => g.OrderBy(e => e.DisplayName));
}
