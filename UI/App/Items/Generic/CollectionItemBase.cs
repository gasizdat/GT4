using GT4.UI.Utils;

namespace GT4.UI.Items;

public abstract class CollectionItemBase<TDto>
{
  private readonly TDto _Info;
  private readonly string _ImageResource;

  protected CollectionItemBase(TDto info, string imageResource)
  {
    _ImageResource = imageResource;
    _Info = info;
  }

  public TDto Info => _Info;

  public virtual ImageSource Icon => ImageUtils.ImageFromRawResource(_ImageResource);
}
