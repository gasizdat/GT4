using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class ProjectItem
{
  private readonly ProjectInfo _Info;
  private ImageSource _DefaultProjectImage => ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync("project_icon.png"));


  public ProjectItem(ProjectInfo info)
  {
    _Info = info;
  }

  public ProjectInfo Info => _Info;
  public string Description => _Info.Description;
  public string Name => _Info.Name;
  public virtual ImageSource ProjectImage => _DefaultProjectImage;
  public virtual bool IsHandlesVisible => true;
}
