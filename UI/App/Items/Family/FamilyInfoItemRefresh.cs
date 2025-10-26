using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class FamilyInfoItemRefresh : FamilyInfoItem
{
  private static string GetName(Exception ex) =>
    string.Format(Resources.UIStrings.BtnNameRefreshAfterError, ex.Message);

  public FamilyInfoItemRefresh(Exception ex)
    : base(new Name(0, GetName(ex), NameType.FamilyName, null), Array.Empty<PersonInfoItem>())
  {
  }

  protected override ImageSource? CustomImage => RefreshItemImage;

  public override bool IsHandlesVisible => false;
}
