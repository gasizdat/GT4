namespace GT4.UI.Abstraction;

public interface IImageCache
{
  void Clear();
  ImageSource GetImage(string key, Func<byte[]> dataProvider);
}
