using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly ISettingEditorsHolder _SettingEditorsHolder;

  // TODO
  // We need to retrieve all **known** formatters to force their registration in ISettingEditorsHolder.
  // This is a bad approach, as the settings page should know nothing about the existing settings.
  // This is a workaround, and we need to find a better solution or even change the entire settings architecture.
  public SettingsPage(
    ISettingEditorsHolder settingEditorsHolder,
    IBiologicalSexFormatter?  biologicalSexFormatter = null,
    IDateFormatter? dateFormatter  = null,
    IDateSpanFormatter? dateSpanFormatter = null,
    INameFormatter? nameFormatter = null,
    INameTypeFormatter? nameTypeFormatter = null,
    IRelationshipTypeFormatter? relationshipTypeFormatter = null)
  {
    _SettingEditorsHolder = settingEditorsHolder;

    InitializeComponent();
  }

  public string DateExample
  {
    // TODO temporary code
    get => _SettingEditorsHolder
      .GetSettingEditors()
      .Single(s=>s.DisplayName == UIStrings.FieldDateDisplayFormat).Example;
  }

  public string DateFormat
  {
    // TODO temporary code
    get => _SettingEditorsHolder
      .GetSettingEditors()
      .Single(s => s.DisplayName == UIStrings.FieldDateDisplayFormat).Value;
    set
    {
      // TODO temporary code
      _SettingEditorsHolder
        .GetSettingEditors()
        .Single(s => s.DisplayName == UIStrings.FieldDateDisplayFormat).Value = value;
      OnPropertyChanged(nameof(DateFormat));
      OnPropertyChanged(nameof(DateExample));
    }
  }

  public string ShortDateExample
  {
    // TODO temporary code
    get => _SettingEditorsHolder
      .GetSettingEditors()
      .Single(s => s.DisplayName == UIStrings.FieldShortDateDisplayFormat).Example;
  }

  public string ShortDateFormat
  {
    // TODO temporary code
    get => _SettingEditorsHolder
      .GetSettingEditors()
      .Single(s => s.DisplayName == UIStrings.FieldShortDateDisplayFormat).Value;
    // TODO temporary code
    set
    {
      _SettingEditorsHolder
      .GetSettingEditors()
      .Single(s => s.DisplayName == UIStrings.FieldShortDateDisplayFormat).Value = value;
      OnPropertyChanged(nameof(ShortDateFormat));
      OnPropertyChanged(nameof(ShortDateExample));
    }
  }
}