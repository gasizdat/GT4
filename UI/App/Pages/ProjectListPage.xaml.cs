using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Collections.ObjectModel;
using System.Text;
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

  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<ProjectInfo> _ProjectInfoComparer;
  private readonly ICommand _PageCommand;
  private readonly IProjectList _ProjectList;
  private readonly IGedcomImporter _Importer;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private readonly ObservableCollection<ProjectItem> _Projects = new();

  public ProjectListPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IComparer<ProjectInfo> projectInfoComparer,
    IProjectList projectList,
    IGedcomImporter importer,
    IAlertService alertService,
    INavigationService navigationService
    )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _ProjectInfoComparer = projectInfoComparer;
    _ProjectList = projectList;
    _Importer = importer;
    _AlertService = alertService;
    _NavigationService = navigationService;
    _PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();
  }

  public ICollection<ProjectItem> Projects => _Projects;

  public ICommand PageCommand => _PageCommand;

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    // async void event handler: an escaped exception is unobserved and crashes the app, so guard it.
    async Task OnNavigateAsync()
    {
      switch (e.CurrentSelection.FirstOrDefault())
      {
        case ProjectItem projectItem:
          {
            using var token = _CancellationTokenProvider.CreateDbCancellationToken();
            await _CurrentProjectProvider.OpenAsync(projectItem.Info, token);
            await _NavigationService.GoToAsync(UIRoutes.GetRoute<ProjectPage>());

            // TODO not so good approach
            if (sender is SelectableItemsView view)
            {
              view.SelectedItem = null;
            }
            break;
          }
      }
    }

    await SafeTask.GuardAsync(OnNavigateAsync, _AlertService);
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    _ = SafeTask.Run(async () =>
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _CurrentProjectProvider.CloseAsync(token);
      await SafeTask.RunOnMainThread(UpdateProjectList, _AlertService);
    }, _AlertService);
  }

  protected async Task UpdateProjectList()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var items = await _ProjectList.GetItemsAsync(token);
    var projects = items
      .Select(projectInfo => new ProjectItem(projectInfo))
      .OrderBy(item => item.Info, _ProjectInfoComparer);

    _Projects.Clear();
    foreach (var project in projects)
    {
      _Projects.Add(project);
    }
  }

  protected async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "Create":
        await OnCreateProject();
        await UpdateProjectList();
        break;
      case string commandName when commandName == "ImportGedcom":
        await OnImportGedcom();
        break;
      case string commandName when commandName == "Refresh":
        this.RefreshView();
        break;
      case string commandName when commandName == "Settings":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<SettingsPage>());
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

    var dialog = new GedcomImportDialog(name);
    await Navigation.PushModalAsync(dialog);
    ProjectInfo? info = null;
    try
    {
      info = await Task.Run(() => RunImportAsync(file, name, description, dialog.Token));
    }
    catch (OperationCanceledException)
    {
      // The user cancelled; RunImportAsync has already deleted the half-built project. Stay on the list.
    }
    finally
    {
      await Navigation.PopModalAsync();
    }

    if (info is null)
      return;

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider.OpenAsync(info, token);
    await _NavigationService.GoToAsync(UIRoutes.GetRoute<ProjectPage>());
  }

  // Runs the whole document operation as one continuous async flow on a background thread, under the
  // dialog's cancellation token rather than a short-lived DB token. Any failure — a user cancellation or a
  // malformed file — deletes the freshly created project shell and rethrows, so nothing is left behind; the
  // caller turns cancellation into a quiet no-op and lets real errors surface through SafeCommand.
  private async Task<ProjectInfo> RunImportAsync(FileResult file, string name, string description, CancellationToken token)
  {
    var host = await _ProjectList.CreateAsync(name, description, token);
    try
    {
      using var stream = await file.OpenReadAsync();
      using var reader = new StreamReader(stream, Encoding.UTF8);
      var mediaBasePath = string.IsNullOrEmpty(file.FullPath) ? null : Path.GetDirectoryName(file.FullPath);
      await _Importer.ImportAsync(host.Project!, reader, token, mediaBasePath);

      var revision = await host.Project!.Metadata.GetProjectRevisionAsync(token) ?? string.Empty;
      await host.DisposeAsync();
      return new ProjectInfo(Revision: revision, Description: description, Name: name, Origin: host.Origin);
    }
    catch
    {
      await host.DisposeAsync();
      using var cleanupToken = _CancellationTokenProvider.CreateDbCancellationToken();
      await _ProjectList.RemoveAsync(host.Origin, cleanupToken);
      throw;
    }
  }
}