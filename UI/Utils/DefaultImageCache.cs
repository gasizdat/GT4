namespace GT4.UI.Utils;

// ImageSource.FromStream results have no stable identity MAUI's platform image cache can key on, so
// each call independently decodes -- fine for a one-off image, but the handful of default stub
// images (male/female/project icon) get re-requested on every PersonInfoView materialization, and
// BindableLayout (used for family-card person rows) never recycles those views. Caching the
// resolved ImageSource here lets repeated requests for the same stub reuse one decode instead of
// re-decoding on every scroll-triggered rebuild. Unlike a per-project photo cache, these are static
// app assets that never change, so entries never need to be evicted or invalidated.
public sealed class DefaultImageCache
{
  private readonly Dictionary<string, ImageSource> _Cache = new();
  private readonly object _Sync = new();

  public ImageSource Get(string resourceName)
  {
    lock (_Sync)
    {
      if (!_Cache.TryGetValue(resourceName, out var source))
      {
        source = ImageUtils.DownsizedImageFromRawResource(resourceName, ImageUtils.PhotoMaxDecodeSize);
        _Cache[resourceName] = source;
      }
      return source;
    }
  }
}
