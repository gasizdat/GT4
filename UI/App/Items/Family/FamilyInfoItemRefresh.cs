using GT4.Core.Project;
using GT4.Core.Project.Dto;

namespace GT4.UI.Items;

public class FamilyInfoItemRefresh : FamilyInfoItem
{
  public FamilyInfoItemRefresh(Exception ex)
    : base(new Name(TableBase.NonCommitedId, GetRefreshOnErrorButtonName(ex), NameType.FamilyName, null), Array.Empty<PersonInfo>())
  {
  }

  protected override ImageSource? CustomImage => RefreshItemImage;

  public override bool IsHandlesVisible => false;
}
