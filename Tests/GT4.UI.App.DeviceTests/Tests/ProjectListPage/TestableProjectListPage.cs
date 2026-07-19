using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Pages;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes ProjectListPage's protected seams. UpdateProjectList is normally reached only through
/// OnNavigatedTo's own fire-and-forget SafeTask.Run; OnPageCommand only through PageCommand, whose
/// fire-and-forget Execute can't be awaited by a test.
/// </summary>
internal sealed class TestableProjectListPage : ProjectListPage
{
  public TestableProjectListPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IComparer<ProjectInfo> projectInfoComparer,
    IProjectList projectList,
    IGedcomImporter importer,
    GedcomImportEncoding gedcomImportEncoding,
    IAlertService alertService,
    INavigationService navigationService)
    : base(
      cancellationTokenProvider,
      currentProjectProvider,
      projectInfoComparer,
      projectList,
      importer,
      gedcomImportEncoding,
      alertService,
      navigationService)
  {
  }

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  public Task InvokeUpdateProjectListAsync() => UpdateProjectList();

  public Task InvokeProjectSelectedAsync(ProjectItem projectItem) => OnProjectSelectedAsync(projectItem);
}
