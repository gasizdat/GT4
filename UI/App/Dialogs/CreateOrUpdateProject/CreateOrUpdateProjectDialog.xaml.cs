using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdateProjectDialog : ContentPage
{
  private readonly TaskCompletionSource<ProjectInfo> _Info = new();
  private readonly ICommand _DialogCommand;
  private string _ProjectDescription = string.Empty;
  private string _ProjectName = string.Empty;
  private string _DialogButtonName;

  public CreateOrUpdateProjectDialog(ProjectInfo? info, IAlertService alertService)
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

    _DialogCommand = new SafeCommand(OnCreateProject, alertService);
    InitializeComponent();
  }

  public ICommand DialogCommand => _DialogCommand;

  public Task<ProjectInfo> ProjectInfo => _Info.Task;

  public string DialogButtonName => 
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
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  private void OnCreateProject()
  {
    _Info.SetResult(new ProjectInfo(
      Description: _ProjectDescription,
      Name: _ProjectName,
      Revision: string.Empty,
      Origin: default!));
  }
}