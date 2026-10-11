using GT4.Core.Utils;

namespace GT4.UI.Utils.Settings;

// How much of a circle the fan chart spans, in degrees.
public static class FanChartSweep
{
  public const int Default = 360;

  public static readonly int[] Options = [180, 270, 360];

  public static SettingKind.Choice Kind => new([.. Options.Select(sweep => new SettingKind.Option($"{sweep}", $"{sweep}°"))]);

  // A hand-edited config can name a sweep that is not one of the offered three, so every read of the
  // persisted string goes through here rather than trusting it.
  public static int Parse(string? sweep) =>
    int.TryParse(sweep, out var parsed) && Options.Contains(parsed) ? parsed : Default;
}
