using GT4.Core.Project;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using System.Collections.ObjectModel;

namespace GT4.UI.Pages;

public partial class ProjectListPage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<ProjectItem> _ProjectItemsComparer;
  private readonly IProjectList _ProjectList;
  private readonly ObservableCollection<ProjectItem> _Projects = new();

  private void UpdateProjectList()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var projects = _ProjectList
      .GetItemsAsync(token)
      .Result
      .Select(projectInfo => new ProjectItem(projectInfo))
      .OrderBy(item => item, _ProjectItemsComparer);

    _Projects.Clear();
    foreach (var project in projects)
    {
      _Projects.Add(project);
    }
    _Projects.Add(new ProjectItemCreate());
  }

  protected ProjectListPage(IServiceProvider services)
  {
    _CancellationTokenProvider = services.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = services.GetRequiredService<ICurrentProjectProvider>();
    _ProjectItemsComparer = services.GetRequiredService<IComparer<ProjectItem>>();
    _ProjectList = services.GetRequiredService<IProjectList>();

    InitializeComponent();
  }

  public ProjectListPage()
    : this(ServiceBuilder.DefaultServices)
  {
  }

  public ICollection<ProjectItem> Projects => _Projects;

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case ProjectItemCreate:
        await OnCreateProject();
        break;

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

  internal async Task OnCreateProject()
  {
    var dialog = new CreateOrUpdateProjectDialog(null);

    await Navigation.PushModalAsync(dialog);
    var projectInfo = await dialog.ProjectInfo;
    await Navigation.PopModalAsync();

    try
    {
      if (projectInfo.Name == string.Empty)
        return;

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await using var project = await _ProjectList.CreateAsync(projectInfo.Name, projectInfo.Description, token);
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
    finally
    {
      UpdateProjectList();
    }
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    UpdateProjectList();
  }
}