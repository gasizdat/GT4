using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Items;

public class ProjectItemCreate : ProjectItem
{
  public ProjectItemCreate()
    : base(new ProjectInfo(UIStrings.BtnNameCreateGenealogyTree, string.Empty, string.Empty))
  {
  }

  public override ImageSource ProjectImage => ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync("add_content.png"));
  public override bool IsHandlesVisible => false;
}