using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class FamilyInfoItemCreate : FamilyInfoItem
{
  public FamilyInfoItemCreate()
    : base(new Name(0, Resources.UIStrings.BtnNameCreateFamily, NameType.FamilyName, null), Array.Empty<PersonInfoItem>())
  {
  }

  public override ImageSource FamilyImage => ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync("add_content.png"));
  public override bool IsHandlesVisible => false;
}
