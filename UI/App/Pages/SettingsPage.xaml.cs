using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly IEnumerable<ISettingEditor> _SettingEditors;

  public SettingsPage(IServiceProvider serviceProvider)
  {
    _SettingEditors = serviceProvider.GetKeyedServices<ISettingEditor>(KeyedService.AnyKey);

    InitializeComponent();
  }

  public string DateExample
  {
    // TODO temporary code
    get => _SettingEditors.Single(s => s.DisplayName == UIStrings.FieldDateDisplayFormat).Example;
  }

  public string DateFormat
  {
    // TODO temporary code
    get => _SettingEditors.Single(s => s.DisplayName == UIStrings.FieldDateDisplayFormat).Value;
    set
    {
      // TODO temporary code
      _SettingEditors.Single(s => s.DisplayName == UIStrings.FieldDateDisplayFormat).Value = value;
      OnPropertyChanged(nameof(DateFormat));
      OnPropertyChanged(nameof(DateExample));
    }
  }

  public string ShortDateExample
  {
    // TODO temporary code
    get => _SettingEditors.Single(s => s.DisplayName == UIStrings.FieldShortDateDisplayFormat).Example;
  }

  public string ShortDateFormat
  {
    // TODO temporary code
    get => _SettingEditors.Single(s => s.DisplayName == UIStrings.FieldShortDateDisplayFormat).Value;
    // TODO temporary code
    set
    {
      _SettingEditors.Single(s => s.DisplayName == UIStrings.FieldShortDateDisplayFormat).Value = value;
      OnPropertyChanged(nameof(ShortDateFormat));
      OnPropertyChanged(nameof(ShortDateExample));
    }
  }
}