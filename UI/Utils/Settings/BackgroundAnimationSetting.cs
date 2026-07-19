using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class BackgroundAnimationSetting : ISettingEditor
{
  private const string BackgroundAnimationSection = "Appearance.BackgroundAnimation";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly BackgroundAnimation? _BackgroundAnimation;

  public BackgroundAnimationSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    BackgroundAnimation? backgroundAnimation)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _BackgroundAnimation = backgroundAnimation;
  }

  public string Group => nameof(BackgroundAnimation);

  public string DisplayName => UIStrings.FieldBackgroundAnimation;

  public string Description => UIStrings.FieldBackgroundAnimationHint;

  public string Example => Value;

  public string Value
  {
    get => _Configuration[BackgroundAnimationSection] ?? "True";
    set
    {
      _BackgroundAnimation?.Apply(value);
      _InteractiveConfiguration?.SetKey(BackgroundAnimationSection, value);
    }
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(BackgroundAnimationSection);
    _BackgroundAnimation?.Apply(Value);
  }
}
