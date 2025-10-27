namespace GT4.UI.App.Items;

public abstract class CollectionItemBase<TDto>
{
  private readonly TDto _Info;

  protected CollectionItemBase(TDto info, string defaultImageResource)
  {
    DefaultImage = ImageFromRawResource(defaultImageResource);
    _Info = info;
  }

  protected virtual ImageSource? CustomImage => null;
  protected readonly ImageSource DefaultImage;
  protected readonly ImageSource CreateItemImage = ImageFromRawResource("add_content.png");
  protected readonly ImageSource RefreshItemImage = ImageFromRawResource("refresh_on_error.png");

  protected static string GetRefreshOnErrorButtonName(Exception ex) =>
    string.Format(Resources.UIStrings.BtnNameRefreshAfterError, ex.Message);

  protected static ImageSource ImageFromBytes(byte[] data) =>
    ImageSource.FromStream(token => Task.Run<Stream>(() => new MemoryStream(data), token));

  protected static ImageSource ImageFromRawResource(string resourceName) =>
    ImageSource.FromFile(resourceName)
    /*ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync(resourceName))*/;

  public TDto Info => _Info;
  public ImageSource Icon => CustomImage ?? DefaultImage;
  public virtual bool IsHandlesVisible => true;
}