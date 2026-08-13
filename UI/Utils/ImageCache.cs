using GT4.UI.Abstraction;
using Microsoft.Extensions.Caching.Memory;

namespace GT4.UI.Utils;

public class ImageCache : IImageCache
{
  private readonly MemoryCache _Cache;

  public ImageCache(int sizeLimit)
  {
    _Cache = new(new MemoryCacheOptions { SizeLimit = sizeLimit });
  }

  // Resolved eagerly so the cached ImageSource closes over the bytes the caller asked for -- a thumbnail
  // stays a thumbnail. Built over the provider instead, it would capture the source Data and keep a
  // full-resolution photo alive for every cached thumbnail. Eager also makes entry.Size the entry's real
  // cost, and keeps one ImageSource per key: Image.Source ignores a reference-equal assignment, so a fresh
  // instance per call would reload the image on every list-cell rebind.
  public ImageSource GetImage(string key, Func<byte[]> dataProvider)
  {
    return CacheExtensions.GetOrCreate(_Cache, key, entry =>
    {
      var bytes = dataProvider();
      entry.Size = bytes.Length;

      return ImageSource.FromStream(() => new MemoryStream(bytes));
    })!;
  }

  public void Clear() => _Cache.Clear();
}
