namespace GT4.Core.Utils;

public enum SettingKind
{
  Text,
  Boolean,
  BoundedNumeric,
}

// Whatever a Kind needs beyond the Value string to drive its control. Which implementation an
// editor carries follows from its Kind.
public interface ISettingMetadata;

// Unit is the suffix Value carries, so an editor can read and write Value without knowing what the
// setting measures.
public sealed record NumericSettingMetadata(double Minimum, double Maximum, double Step, string Unit)
  : ISettingMetadata;

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
  SettingKind Kind => SettingKind.Text;

  ISettingMetadata? Metadata => null;

  // Reset setting to default
  void ResetToDefault();
}
