using GT4.UI.Abstraction;
using GT4.UI.Utils.Converters;
using SkiaSharp;

namespace GT4.UI.Utils.Extensions;

public static class PhotoEnhancementExtensions
{
  public static async Task<PhotoInfo> EnhanceAsync(this IPhotoEnhancement enhancement, PhotoInfo photo, CancellationToken token)
  {
    // Every IDataConverter hands out a StreamImageSource.
    var source = (StreamImageSource)photo.Source;
    using var stream = await source.Stream(token);
    using var bitmap = ImageUtils.DecodeUpright(stream) ?? throw new InvalidOperationException("Could not decode the photo.");
    using var enhanced = await enhancement.ApplyAsync(bitmap, token);
    using var image = SKImage.FromBitmap(enhanced);
    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
    var bytes = encoded.ToArray();

    return photo with { Source = ImageSource.FromStream(() => new MemoryStream(bytes)) };
  }
}
