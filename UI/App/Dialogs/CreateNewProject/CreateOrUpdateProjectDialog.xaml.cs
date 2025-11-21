using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdateProjectDialog : ContentPage
{
  private readonly TaskCompletionSource<ProjectInfo> _Info = new();
  private string _ProjectDescription = string.Empty;
  private string _ProjectName = string.Empty;
  private string _DialogButtonName;

  public CreateOrUpdateProjectDialog(ProjectInfo? info)
  {
    if (info is not null)
    {
      _ProjectDescription = info.Description;
      _ProjectName = info.Name;
      _DialogButtonName = UIStrings.BtnNameUpdateGenealogyTree;
    }
    else
    {
      _DialogButtonName = UIStrings.BtnNameCreateGenealogyTree;
    }

    InitializeComponent();
  }

  public void OnCreateProjectBtn(object sender, EventArgs e)
  {
    _Info.SetResult(new ProjectInfo(Description: _ProjectDescription, Name: _ProjectName, Origin: default!));
  }

  public Task<ProjectInfo> ProjectInfo => _Info.Task;

  public string CreateProjectBtName => 
    string.IsNullOrWhiteSpace(_ProjectName) ? UIStrings.BtnNameCancel : _DialogButtonName;

  public string ProjectDescription
  {
    get => _ProjectDescription;
    set => _ProjectDescription = value;
  }

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