using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Utils;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectListPage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<ProjectInfo> _ProjectInfoComparer;
  private readonly ICommand _PageCommand;
  private readonly IProjectList _ProjectList;
  private readonly ObservableCollection<ProjectItem> _Projects = new();

  public ProjectListPage(IServiceProvider services)
  {
    _CancellationTokenProvider = services.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = services.GetRequiredService<ICurrentProjectProvider>();
    _ProjectInfoComparer = services.GetRequiredService<IComparer<ProjectInfo>>();
    _PageCommand = new SafeCommand(OnPageCommand);
    _ProjectList = services.GetRequiredService<IProjectList>();

    InitializeComponent();
  }

  public ICollection<ProjectItem> Projects => _Projects;

  public ICommand PageCommand => _PageCommand;

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case ProjectItem projectItem:
        {
          using var token = _CancellationTokenProvider.CreateDbCancellationToken();
          await _CurrentProjectProvider.OpenAsync(projectItem.Info, token);
          await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectPage>());

          // TODO not so good approach
          if (sender is SelectableItemsView view)
          {
            view.SelectedItem = null;
          }
          break;
        }
    }
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    UpdateProjectList();
  }

  private void UpdateProjectList()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var projects = _ProjectList
      .GetItemsAsync(token)
      .Result
      .Select(projectInfo => new ProjectItem(projectInfo))
      .OrderBy(item => item.Info, _ProjectInfoComparer);

    _Projects.Clear();
    foreach (var project in projects)
    {
      _Projects.Add(project);
    }
  }

  private async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "Create":
        await OnCreateProject();
        UpdateProjectList();
        break;
      case string commandName when commandName == "Refresh":
        this.RefreshView();
        break;
      case string commandName when commandName == "Settings":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<SettingsPage>());
        break;
    }
  }

  private async Task OnCreateProject()
  {
    var dialog = new CreateOrUpdateProjectDialog(null);

    await Navigation.PushModalAsync(dialog);
    var projectInfo = await dialog.ProjectInfo;
    await Navigation.PopModalAsync();

    if (projectInfo.Name == string.Empty)
      return;

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await using var project = await _ProjectList.CreateAsync(projectInfo.Name, projectInfo.Description, token);
  }
}