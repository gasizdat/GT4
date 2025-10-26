namespace GT4.UI.App.Items;

public class FamilyMemberInfoItemCreate : FamilyMemberInfoItem
{
  public FamilyMemberInfoItemCreate()
    : base(Resources.UIStrings.BtnNameCreateFamilyPerson, ServiceBuilder.DefaultServices)
  {

  }

  protected override ImageSource? CustomImage => CreateItemImage;

  public override bool IsHandlesVisible => false;
}
