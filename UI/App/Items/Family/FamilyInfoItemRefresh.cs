using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class FamilyInfoItemRefresh : FamilyInfoItem
{
  public FamilyInfoItemRefresh(Exception ex)
    : base(new Name(0, GetRefreshOnErrorButtonName(ex), NameType.FamilyName, null), Array.Empty<PersonInfoItem>())
  {
  }

  protected override ImageSource? CustomImage => RefreshItemImage;

  public override bool IsHandlesVisible => false;
}
