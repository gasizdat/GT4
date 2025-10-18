namespace GT4.UI;

using GT4.Core.Project;
using GT4.UI.Resources;

public partial class CreateNewProjectDialog : ContentPage
{
  private string _projectName = string.Empty;
  private TaskCompletionSource<ProjectInfo> _info = new();

  public CreateNewProjectDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public void OnCreateProjectBtn(object sender, EventArgs e)
  {
    _info.SetResult(new ProjectInfo { Description = ProjectDescription, Name = ProjectName });
  }

  public Task<ProjectInfo> ProjectInfo => _info.Task;

  public string CreateProjectBtName => string.IsNullOrWhiteSpace(ProjectName) ? UIStrings.BtnNameCancel : UIStrings.BtnNameCreateGenealogyTree;
  public string ProjectDescription { get; set; } = string.Empty;
  public string ProjectName
  {
    get => _projectName;
    set
    {
      _projectName = value;
      OnPropertyChanged(nameof(CreateProjectBtName));
    }
  }
}