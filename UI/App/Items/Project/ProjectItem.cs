using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Items;

public class ProjectItem : CollectionItemBase<ProjectInfo>
{
  public ProjectItem(ProjectInfo info)
    : base (info, "project_icon.png")
  {
  }

  public string Description => Info.Description;

  public string Name => Info.Name;

  public string Revision => string.IsNullOrWhiteSpace(Info.Revision) 
    ? string.Empty 
    : string.Format(UIStrings.FieldRevision_1, Info.Revision);

  public bool DescriptionVisible => !string.IsNullOrWhiteSpace(Description);

  public bool RevisionVisible => !string.IsNullOrWhiteSpace(Revision);
}
