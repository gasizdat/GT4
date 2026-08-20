namespace GT4.Core.Utils;

// What a setting's Value holds, together with whatever the control editing it needs beyond the
// string itself.
public abstract record SettingKind
{
  public sealed record Text : SettingKind;

  public sealed record Boolean : SettingKind;

  // Unit is the suffix Value carries, so an editor can read and write Value without knowing what
  // the setting measures.
  public sealed record BoundedNumeric(double Minimum, double Maximum, double Step, string Unit)
    : SettingKind;
}

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

  // Decides the control the setting is edited with.
  SettingKind Kind => new SettingKind.Text();

  // Reset setting to default
  void ResetToDefault();
}
