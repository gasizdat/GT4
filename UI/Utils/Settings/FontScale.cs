namespace GT4.UI.Utils.Settings;

// Applies the font-size multiplier to the shared font-size resources so a single factor rescales
// every font in the app. Consumers reference these keys via DynamicResource, so overwriting a key
// here updates the live UI without rebuilding the Shell. This is the only type that rescales the
// resource tokens; FontScaleSetting drives it by calling Apply directly when the user changes the
// setting, and App calls Apply once at startup with the persisted value.
//
// The font-size tokens are authored in Styles.xaml as OnIdiom<double> values; Initialize resolves
// each to the current device's base size once (before it is overwritten) and keeps it, so a later
// Apply can recompute base * factor from the original baseline.
public sealed class FontScale
{
  public const double DefaultFactor = 1.0;
  public const double MinFactor = 0.75;
  public const double MaxFactor = 2.0;

  // Every resource key whose value is a font size. Keep in sync with the tokens in Styles.xaml.
  private static readonly string[] ScaledKeys =
  [
    "LabelTextSizeDefault",
    "LabelTextSizeMedium",
    "LabelTextSizeLarge",
    "LabelTextSizeHuge",
    "LabelTextSizeGiant",
    "ButtonTextSize",
    "ActionButtonTextSize",
    "AdornerButtonTextSize",
    "InputControlTextSize",
  ];

  private readonly Dictionary<string, double> _BaseSizes = new();

  // The currently applied factor. Exposed so code-built views that are not part of the
  // resource/DynamicResource pipeline (e.g. the family-tree nodes) can honour the same scale; those
  // views receive this instance through their construction and read the factor directly.
  public double CurrentFactor { get; private set; } = DefaultFactor;

  // Cache the device-resolved base sizes, then apply the persisted factor. Must be called after the
  // application resources (Styles.xaml) have been merged — i.e. after App.InitializeComponent.
  public void Initialize()
  {
    var resources = Application.Current?.Resources;
    if (resources is null)
    {
      return;
    }

    foreach (var key in ScaledKeys)
    {
      if (resources.TryGetValue(key, out var value) && TryResolveSize(value, out var baseSize))
      {
        _BaseSizes[key] = baseSize;
      }
    }
  }

  // Rescale every font-size resource to base * factor. DynamicResource consumers refresh immediately.
  public void Apply(double? factor)
  {
    if (factor is not null)
    {
      factor = Math.Clamp(factor.Value, MinFactor, MaxFactor);
    }
    CurrentFactor = factor ?? DefaultFactor;

    var resources = Application.Current?.Resources;
    if (resources is null)
    {
      return;
    }

    foreach (var (key, baseSize) in _BaseSizes)
    {
      resources[key] = baseSize * factor;
    }
  }

  public void Apply(string? factorInPercent)
  {
    factorInPercent = factorInPercent?.TrimEnd('%').Trim();
    int? fontScaleFactor = int.TryParse(factorInPercent, out var value) ? value : null;
    Apply(0.01 * fontScaleFactor);
  }

  private static bool TryResolveSize(object value, out double size)
  {
    switch (value)
    {
      // Already resolved/overwritten to a plain size.
      case double resolved:
        size = resolved;
        return true;
      // The authored token: the implicit operator resolves it for the current device idiom.
      case OnIdiom<double> onIdiom:
        size = onIdiom;
        return true;
      default:
        size = default;
        return false;
    }
  }
}
