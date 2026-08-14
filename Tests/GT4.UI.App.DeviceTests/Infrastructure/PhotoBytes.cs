using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Reads back the bytes an already-converted image renders from. A converter's job includes stripping
/// the GedcomPhotoResidue envelope, and only the bytes prove it happened -- asserting that an
/// ImageSource merely exists would pass on an unstripped one just the same.
/// </summary>
internal static class PhotoBytes
{
  public static async Task<byte[]> ReadAsync(ImageSource imageSource)
  {
    var source = Assert.IsType<StreamImageSource>(imageSource);
    await using var stream = await source.Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    return buffer.ToArray();
  }
}
