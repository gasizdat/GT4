namespace GT4.UI.Utils;

internal sealed class ImageDownsizer : IImageDownsizer
{
  public byte[] Downsize(byte[] content, float maxSize) => ImageUtils.DownsizedPng(content, maxSize);
}
