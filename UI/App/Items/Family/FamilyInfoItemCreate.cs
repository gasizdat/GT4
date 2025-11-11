using GT4.Core.Project;
using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class FamilyInfoItemCreate : FamilyInfoItem
{
  public FamilyInfoItemCreate()
    : base(new Name(TableBase.NonCommitedId, Resources.UIStrings.BtnNameCreateFamily, NameType.FamilyName, null), Array.Empty<PersonInfoItem>())
  {
  }

  protected override ImageSource? CustomImage => CreateItemImage;

  public override bool IsHandlesVisible => false;
}
