namespace GT4.UI.Utils;

/// <summary>
/// Downsizes an encoded image to a thumbnail. Abstracted so the platform-bound (MAUI PlatformImage) decode
/// can be substituted — notably in tests, where that path cannot run headless.
/// </summary>
public interface IImageDownsizer
{
  byte[] Downsize(byte[] content, float maxSize);
}
