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
  public override ImageSource FamilyImage => ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync("refresh_on_error.png"));
  public override bool IsHandlesVisible => false;
}
