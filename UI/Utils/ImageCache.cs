using GT4.UI.Abstraction;
using Microsoft.Extensions.Caching.Memory;

namespace GT4.UI.Utils;

public class ImageCache : IImageCache
{
  private readonly MemoryCache _Images;
  private readonly MemoryCache _Bytes;

  public ImageCache(int sizeLimit)
  {
    _Images = new(new MemoryCacheOptions());
    _Bytes = new(new MemoryCacheOptions { SizeLimit = sizeLimit });
  }

  public ImageSource GetImage(string key, Func<byte[]> dataProvider)
  {
    return CacheExtensions.GetOrCreate(_Images, key, entry =>
    {
      entry.Size = 1;
      return ImageSource.FromStream(async _ =>
      {
        var cachedData = CacheExtensions.GetOrCreate(_Bytes, key, async dataEntry =>
        {
          var bytes = dataProvider();
          dataEntry.Size = bytes.Length;

          return bytes;
        });

        return new MemoryStream(cachedData is null ? ImageUtils.TransparentPngData : await cachedData);
      });
    }) ?? ImageUtils.TransparentImageStub;
  }

  public void Clear()
  {
    _Bytes.Clear();
    _Images.Clear();
  }
}
