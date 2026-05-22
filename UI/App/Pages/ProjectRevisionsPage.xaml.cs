using GT4.Core.Project.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectRevisionsPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private DateTimeItem? _SelectedRevision;

  public ProjectRevisionsPage(ICurrentProjectProvider currentProjectProvider)
  {
    _CurrentProjectProvider = currentProjectProvider;

    InitializeComponent();
  }

  public DateTimeItem? SelectedRevision
  {
    get => _SelectedRevision;
    set
    {
      _SelectedRevision = value;
      OnPropertyChanged(nameof(RestoreBtnName));
    }
  }

  public string RestoreBtnName => SelectedRevision is null ?
    UIStrings.BtnNameCancel :
    string.Format(UIStrings.BtnNameRestore_1, SelectedRevision?.DateTimeText);

  public ICommand PageCommand => new SafeCommand(async (object arg) =>
  {
    switch (arg)
    {
      case string commandName when commandName == "Restore":
        if (SelectedRevision is null)
        {
          await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectPage>());
        }
        else
        {

        }
        break;
      case string commandName when commandName == "Refresh":
        this.RefreshView();
        break;
    }
  });

  public ICommand DeleteRevisionCommand => new SafeCommand(() =>
  {
  });

  public IEnumerable<DateTimeItem> Revisions => _CurrentProjectProvider
    .Revisions
    .Select(d => new DateTimeItem(d));
}