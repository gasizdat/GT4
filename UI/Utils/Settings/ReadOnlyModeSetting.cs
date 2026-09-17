using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class ReadOnlyModeSetting : ISettingEditor
{
  private const string ReadOnlyModeSection = "Editing.ReadOnlyMode";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly ReadOnlyMode? _ReadOnlyMode;

  public ReadOnlyModeSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    ReadOnlyMode? readOnlyMode)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _ReadOnlyMode = readOnlyMode;
  }

  public string Group => nameof(ReadOnlyMode);

  public string DisplayName => UIStrings.FieldReadOnlyMode;

  public string Description => UIStrings.FieldReadOnlyModeHint;

  public string Example => Value;

  public string Value
  {
    get => _Configuration[ReadOnlyModeSection] ?? "False";
    set
    {
      _ReadOnlyMode?.Apply(value);
      _InteractiveConfiguration?.SetKey(ReadOnlyModeSection, value);
    }
  }

  public SettingKind Kind => new SettingKind.Boolean();

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(ReadOnlyModeSection);
    _ReadOnlyMode?.Apply(Value);
  }
}
