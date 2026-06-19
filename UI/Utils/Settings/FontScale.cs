namespace GT4.UI.Utils.Settings;

// Applies the font-size multiplier owned by FontScaleSetting to the shared font-size resources so a
// single factor rescales every font in the app. Consumers reference these keys via DynamicResource,
// so overwriting a key here updates the live UI without rebuilding the Shell. This type is the only
// thing that knows how to apply a factor; it reacts to FontScaleSetting.Changed to stay in sync.
//
// The font-size tokens are authored in Styles.xaml as OnIdiom<double> values; Initialize resolves
// each to the current device's base size once (before it is overwritten) and keeps it, so a later
// Apply can recompute base * factor from the original baseline.
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

  private readonly Dictionary<string, double> _BaseSizes = new();

  // The currently applied factor. Mirrored statically so code-built views that are not part of the
  // resource/DynamicResource pipeline (e.g. the family-tree nodes) can honour the same scale without
  // threading the service through their construction.
  public static double CurrentFactor { get; private set; } = FontScaleSetting.DefaultFactor;

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
    CurrentFactor = factor ?? FontScaleSetting.DefaultFactor;

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

  public void Apply(string? factor)
  {
    double? fontScaleFactor = double.TryParse(factor, out var value) ? value : null;
    Apply(fontScaleFactor);
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
