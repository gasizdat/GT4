using GT4.Core.Project.Dto;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Logic;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectListPage : ContentPage
{
  // GEDCOM has no standard MIME type: Windows filters on the ".ged" extension, while Android has none, so
  // it falls back to any file. A picked file always lands in a brand-new project, so this only governs
  // which files are easy to select.
  private static readonly FilePickerFileType GedcomFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
  {
    [DevicePlatform.WinUI] = [".ged"],
    [DevicePlatform.Android] = ["*/*"],
  });

  private readonly ProjectListLogic _Logic;
  private readonly ICommand _PageCommand;
  private readonly ObservableCollection<ProjectItem> _Projects = new();

  public ProjectListPage(IServiceProvider services)
  {
    _Logic = services.GetRequiredService<ProjectListLogic>();
    _PageCommand = new SafeCommand(OnPageCommand);

    InitializeComponent();
  }

  public ICollection<ProjectItem> Projects => _Projects;

  public ICommand PageCommand => _PageCommand;

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    // async void event handler: an escaped exception is unobserved and crashes the app, so guard it.
    try
    {
      switch (e.CurrentSelection.FirstOrDefault())
      {
        case ProjectItem projectItem:
          {
            await _Logic.OpenAsync(projectItem.Info);
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
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      await this.ShowErrorAsync(ex);
    }
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    _ = SafeTask.Run(async () =>
    {
      await _Logic.CloseCurrentAsync();
      var projects = await _Logic.GetProjectsAsync();
      await SafeTask.RunOnMainThread(() => ReloadProjects(projects));
    });
  }

  private void ReloadProjects(ProjectInfo[] projects)
  {
    _Projects.Clear();
    foreach (var project in projects)
    {
      _Projects.Add(new ProjectItem(project));
    }
  }

  private async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "Create":
        await OnCreateProject();
        break;
      case string commandName when commandName == "ImportGedcom":
        await OnImportGedcom();
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

    await _Logic.CreateProjectAsync(projectInfo.Name, projectInfo.Description);
    var projects = await _Logic.GetProjectsAsync();
    ReloadProjects(projects);
  }

  // Imports a GEDCOM file into a fresh project. The importer can merge into a populated document, but from
  // the project list there is nothing open to merge into, so each import deliberately gets its own new
  // project; merging into an existing project is offered from ProjectPage instead. The import can be slow,
  // so it runs on a background thread behind a modal that lets the user cancel it; on success the project
  // is opened as current and we navigate straight into it.
  private async Task OnImportGedcom()
  {
    var pickOptions = new PickOptions { PickerTitle = UIStrings.FileDialogSelectGedcom, FileTypes = GedcomFileType };
    var file = await FilePicker.Default.PickAsync(pickOptions);
    if (file is null)
      return;

    var name = Path.GetFileNameWithoutExtension(file.FileName);
    var description = UIStrings.HintImportedFromGedcom;
    var mediaBasePath = string.IsNullOrEmpty(file.FullPath) ? null : Path.GetDirectoryName(file.FullPath);

    var dialog = new GedcomImportDialog(name);
    await Navigation.PushModalAsync(dialog);
    ProjectInfo? info = null;
    try
    {
      using var stream = await file.OpenReadAsync();
      info = await Task.Run(() => _Logic.ImportAsync(stream, name, description, mediaBasePath, dialog.Token));
    }
    catch (OperationCanceledException)
    {
      // The user cancelled; ImportAsync has already deleted the half-built project. Stay on the list.
    }
    finally
    {
      await Navigation.PopModalAsync();
    }

    if (info is null)
      return;

    await _Logic.OpenAsync(info);
    await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectPage>());
  }
}
