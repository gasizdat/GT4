namespace GT4.UI;

public partial class CreateNewProjectDialog : ContentPage
{
  private TaskCompletionSource<Project.ProjectInfo> _info = new();

  public CreateNewProjectDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public void OnCreateProjectBtn(object sender, EventArgs e)
  {
    _info.SetResult(new Project.ProjectInfo { Description = ProjectDescription, Name = ProjectName });
  }

  public Task<Project.ProjectInfo> ProjectInfo => _info.Task;

  public string CreateProjectBtName => "Create Genealogy Tree";
  public string DialogTitle => "Create a Genealogy Tree";
  public string ProjectDescriptionPlaceholder => "Enter a description for the new Genealogy Tree";
  public string ProjectDescription { get; set; } = string.Empty;
  public string ProjectNamePlaceholder => "Enter a name for the new Genealogy Tree";
  public string ProjectName { get; set; } = string.Empty;
}