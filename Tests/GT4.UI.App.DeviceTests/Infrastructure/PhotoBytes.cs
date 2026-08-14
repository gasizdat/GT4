using GT4.UI.Utils.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Reads back the bytes an already-converted <see cref="PhotoInfo"/> renders from. A converter's job
/// includes stripping the GedcomPhotoResidue envelope, and only the bytes prove it happened -- asserting
/// that a PhotoInfo merely exists would pass on an unstripped one just the same.
/// </summary>
internal static class PhotoBytes
{
  public static async Task<byte[]> ReadAsync(PhotoInfo photo)
  {
    var source = Assert.IsType<StreamImageSource>(photo.Source);
    await using var stream = await source.Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    return buffer.ToArray();
  }
}
