using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewProjectDialog : ContentPage
{
  private string _ProjectName = string.Empty;
  private TaskCompletionSource<ProjectInfo> _Info = new();

  public CreateNewProjectDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public void OnCreateProjectBtn(object sender, EventArgs e)
  {
    _Info.SetResult(new ProjectInfo(Description: ProjectDescription, Name: ProjectName, Origin: default!));
  }

  public Task<ProjectInfo> ProjectInfo => _Info.Task;

  public string CreateProjectBtName => string.IsNullOrWhiteSpace(ProjectName) ? UIStrings.BtnNameCancel : UIStrings.BtnNameCreateGenealogyTree;
  public string ProjectDescription { get; set; } = string.Empty;
  public string ProjectName
  {
    get => _ProjectName;
    set
    {
      _ProjectName = value;
      OnPropertyChanged(nameof(CreateProjectBtName));
    }
  }
}