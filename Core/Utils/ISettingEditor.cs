namespace GT4.Core.Utils;

public interface ISettingEditor
{
  // The group to which this setting belongs.
  // All settings that belong to one group are usually grouped together in the UI.
  string Group { get; }

  // The user-readable, localized name of the setting.
  string DisplayName { get; }

  // The user-readable, localized description of the setting.
  string Description { get; }

  // An interactive example of a parameter that is determined by the settings.
  string Example { get; }

  // The current value of the setting.
  string Value { get; set; }

  // Reset setting to default
  void ResetToDefault();
}
