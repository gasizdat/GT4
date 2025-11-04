using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Items;

public class ProjectItemCreate : ProjectItem
{
  public ProjectItemCreate()
    : base(new ProjectInfo(UIStrings.BtnNameCreateGenealogyTree, string.Empty, default!))
  {
  }

  protected override ImageSource? CustomImage => CreateItemImage;

  public override bool IsHandlesVisible => false;
}