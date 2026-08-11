using GT4.UI.Utils;

namespace GT4.UI.Items;

public abstract class CollectionItemBase<TDto>
{
  private readonly TDto _Info;
  private readonly string _ImageResource;
  private readonly ImageUtils _ImageUtils;

  protected CollectionItemBase(TDto info, string imageResource, ImageUtils imageUtils)
  {
    _Info = info;
    _ImageResource = imageResource;
    _ImageUtils = imageUtils;
  }

  public TDto Info => _Info;

  public virtual ImageSource Icon => _ImageUtils.ImageFromRawResource(_ImageResource, ImageUtils.ThumbnailSize);
}
