namespace GT4.UI.App.Items;

public class FamilyMemberInfoItemRefresh : FamilyMemberInfoItem
{
  public FamilyMemberInfoItemRefresh(Exception ex)
    : base(GetRefreshOnErrorButtonName(ex), ServiceBuilder.DefaultServices)
  {

  }

  protected override ImageSource? CustomImage => RefreshItemImage;

  public override bool IsHandlesVisible => false;
}
