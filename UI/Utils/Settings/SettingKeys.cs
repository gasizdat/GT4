using GT4.Core.Utils;

namespace GT4.UI.Utils.Settings;

/// <summary>
/// Public DI keys for keyed <see cref="Core.Utils.ISettingEditor"/> registrations whose backing type
/// is <c>internal</c> (so a consumer in another assembly, e.g. <c>GT4.UI.App</c>, cannot itself write
/// <c>nameof(FontScaleSetting)</c>). Defined here -- inside the assembly that owns the type -- so this
/// is the one place a rename would need updating, rather than a bare string literal repeated at every
/// call site.
/// </summary>
public static class SettingKeys
{
  public const string FontScale = nameof(FontScaleSetting);
  public const string BackgroundAnimation = nameof(BackgroundAnimationSetting);
}

/// <summary>Resolves every registered <see cref="ISettingEditor"/> regardless of key -- the typed
/// equivalent of <c>GetKeyedServices(KeyedService.AnyKey)</c>, kept out of consumers like
/// <c>SettingsPage</c>.</summary>
public delegate IEnumerable<ISettingEditor> SettingEditorsResolver();
