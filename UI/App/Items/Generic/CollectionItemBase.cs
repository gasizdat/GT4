namespace GT4.UI.App.Items;

public abstract class CollectionItemBase<TDto>
{
  private readonly TDto _Info;

  protected CollectionItemBase(TDto info, string defaultImageResource)
  {
    DefaultImage = ImageUtils.ImageFromRawResource(defaultImageResource);
    _Info = info;
  }

  protected virtual ImageSource? CustomImage => null;
  protected readonly ImageSource DefaultImage;
  protected readonly ImageSource CreateItemImage = ImageUtils.ImageFromRawResource("add_content.png");
  protected readonly ImageSource RefreshItemImage = ImageUtils.ImageFromRawResource("refresh_on_error.png");

  protected static string GetRefreshOnErrorButtonName(Exception ex) =>
    string.Format(Resources.UIStrings.BtnNameRefreshAfterError, ex.Message);

  public TDto Info => _Info;
  public ImageSource Icon => CustomImage ?? DefaultImage;
  public virtual bool IsHandlesVisible => true;
}