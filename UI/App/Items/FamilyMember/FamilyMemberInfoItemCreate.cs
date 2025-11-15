namespace GT4.UI.Items;

public class FamilyMemberInfoItemCreate : FamilyMemberInfoItem
{
  public FamilyMemberInfoItemCreate(IServiceProvider serviceProvider)
    : base(Resources.UIStrings.BtnNameCreateFamilyPerson, serviceProvider)
  {
  }

  public FamilyMemberInfoItemCreate()
    : this(ServiceBuilder.DefaultServices)
  {

  }

  protected override ImageSource? CustomImage => CreateItemImage;

  public override bool IsHandlesVisible => false;
}
