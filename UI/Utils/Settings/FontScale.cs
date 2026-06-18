namespace GT4.UI.Utils.Settings;

// Applies the persisted font-size multiplier (FontScaleSetting) to the shared font-size resources so
// a single factor rescales every font in the app. Consumers reference these keys via DynamicResource,
// so overwriting a key here updates the live UI without rebuilding the Shell.
//
// The font-size tokens are authored in Styles.xaml as OnIdiom<double> values; this service resolves
// each to the current device's base size once (before it is overwritten) and keeps it, so a later
// factor change can recompute base * factor from the original baseline.
public sealed class FontScale
{
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

  private readonly FontScaleSetting _Setting;
  private readonly Dictionary<string, double> _BaseSizes = new();

  public FontScale(FontScaleSetting setting)
  {
    _Setting = setting;
  }

  // The currently applied factor. Mirrored statically so code-built views that are not part of the
  // resource/DynamicResource pipeline (e.g. the family-tree nodes) can honour the same scale without
  // threading the service through their construction.
  public static double CurrentFactor { get; private set; } = FontScaleSetting.DefaultFactor;

  public double Factor => _Setting.Value;

  // Cache the device-resolved base sizes, then apply the persisted factor. Must be called after the
  // application resources (Styles.xaml) have been merged — i.e. after App.InitializeComponent.
  public void Initialize()
  {
    var resources = Application.Current?.Resources;
    if (resources is null)
      return;

    foreach (var key in ScaledKeys)
    {
      if (resources.TryGetValue(key, out var value) && TryResolveSize(value, out var baseSize))
        _BaseSizes[key] = baseSize;
    }

    Apply(_Setting.Value);
  }

  // Persist and apply a new factor. DynamicResource consumers refresh immediately.
  public void SetFactor(double factor)
  {
    factor = FontScaleSetting.Clamp(factor);
    _Setting.Value = factor;
    Apply(factor);
  }

  // Drop the persisted factor and fall back to the unscaled baseline.
  public void ResetToDefault()
  {
    _Setting.ResetToDefault();
    Apply(_Setting.Value);
  }

  private void Apply(double factor)
  {
    CurrentFactor = factor;

    var resources = Application.Current?.Resources;
    if (resources is null)
      return;

    foreach (var (key, baseSize) in _BaseSizes)
      resources[key] = baseSize * factor;
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
