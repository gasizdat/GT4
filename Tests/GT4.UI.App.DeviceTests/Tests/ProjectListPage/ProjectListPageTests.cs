using GT4.Core.Project;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Pages;
using GT4.UI.Resources;
using Moq;
using Xunit;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers ProjectListPage against a real MAUI runtime with a mocked Core. PageCommand("Create")
/// pushes a modal CreateOrUpdateProjectDialog, driven through WindowHost/ModalDialogHarness.
/// ImportGedcom is not covered (FilePicker is an external dependency, same rationale as
/// ProjectPage.ExportGedcom/ImportGedcom); the demo project takes the same import path from a
/// bundled asset instead, so it is.
/// </summary>
public class ProjectListPageTests
{
  private static ProjectInfo P(string name, string description = "") => new(
    Name: name,
    Description: description,
    Revision: null,
    Origin: new FileDescription(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, []), $"{name}.gt4", null));

  private static ProjectHost CreateHost(ProjectInfo info) =>
    new(Mock.Of<IFileSystem>(), info.Origin, info.Origin);

  private static async Task<TestableProjectListPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableProjectListPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.Empty(page.Projects);
  }

  [Fact]
  public async Task UpdateProjectList_sorts_with_the_registered_comparer()
  {
    var services = new TestServices();
    var unsorted = new[] { P("Pushkin"), P("Aksakov"), P("Tolstoy") };
    services.ProjectList.Setup(p => p.GetItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(unsorted);
    var page = await CreatePageAsync(services);

    await MainThread.InvokeOnMainThreadAsync(page.InvokeUpdateProjectListAsync);
    var projects = await MainThread.InvokeOnMainThreadAsync(() => page.Projects.ToArray());

    Assert.Equal(["Aksakov", "Pushkin", "Tolstoy"], projects.Select(p => p.Name));
  }

  [Fact]
  public async Task CreateProject_modal_creates_the_project()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    ProjectHost? host = null;
    services.ProjectList
      .Setup(p => p.CreateAsync("New Tree", "A description", It.IsAny<CancellationToken>()))
      .ReturnsAsync(() => host = CreateHost(P("New Tree", "A description")));

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("Create"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateProjectDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.ProjectName = "New Tree";
      dialog.ProjectDescription = "A description";
      dialog.DialogCommand.Execute(null);
    });
    await commandTask;

    services.ProjectList.Verify(
      p => p.CreateAsync("New Tree", "A description", It.IsAny<CancellationToken>()), Times.Once());
    Assert.NotNull(host);
  }

  [Fact]
  public async Task CreateProject_cancelled_dialog_does_not_create_a_project()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("Create"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateProjectDialog>(page);

    // Leaving ProjectName empty makes the dialog's own command produce Name ==
    // string.Empty, which ProjectListPage treats as cancelled.
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    await commandTask;

    services.ProjectList.Verify(
      p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task SelectProject_opens_it_navigates_and_clears_selection()
  {
    var services = new TestServices();
    var info = P("Pushkin");
    services.CurrentProjectProvider
      .Setup(p => p.OpenAsync(info, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var page = await CreatePageAsync(services);
    var projectItem = new ProjectItem(info);

    await page.InvokeProjectSelectedAsync(projectItem);

    services.CurrentProjectProvider.Verify(p => p.OpenAsync(info, It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(n => n.GoToAsync($"{typeof(ProjectPage).Namespace}/{typeof(ProjectPage).Name}"), Times.Once());
    Assert.Null(page.SelectedProject);
  }

  [Fact]
  public async Task Empty_list_shows_the_empty_state()
  {
    var page = await CreatePageAsync(new TestServices());

    await MainThread.InvokeOnMainThreadAsync(page.InvokeUpdateProjectListAsync);

    Assert.Empty(page.Projects);
    Assert.True(page.IsEmptyStateVisible);
  }

  [Fact]
  public async Task Populated_list_hides_the_empty_state()
  {
    var services = new TestServices();
    services.ProjectList.Setup(p => p.GetItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([P("Pushkin")]);
    var page = await CreatePageAsync(services);

    await MainThread.InvokeOnMainThreadAsync(page.InvokeUpdateProjectListAsync);

    Assert.Single(page.Projects);
    Assert.False(page.IsEmptyStateVisible);
  }

  [Fact]
  public async Task Empty_state_stays_hidden_until_the_list_has_been_read()
  {
    // An empty collection means "nothing to show" only after the load; during it the page knows nothing
    // yet, and the offer would flash over the activity indicator on a machine that does have projects.
    var page = await CreatePageAsync(new TestServices());

    page.Loading.IsLoading = true;

    Assert.False(page.IsEmptyStateVisible);
  }

  [Fact]
  public async Task Sanitize_failure_does_not_stop_the_listing()
  {
    // The revision sweep shares the listing's 5s DB token and is pure housekeeping: exhausting that
    // budget, or tripping over a file another process holds, must never keep the project list from
    // loading - which is the one screen the user cannot route around.
    var services = new TestServices();
    services.ProjectList
      .Setup(p => p.SanitizeRevisionsAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());
    services.ProjectList.Setup(p => p.GetItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([P("Pushkin")]);
    var page = await CreatePageAsync(services);

    await page.InvokeSanitizeRevisionsAsync(CancellationToken.None);
    await MainThread.InvokeOnMainThreadAsync(page.InvokeUpdateProjectListAsync);

    Assert.Single(page.Projects);
  }

  [Fact]
  public async Task Demo_imports_the_bundled_gedcom_into_a_new_project_and_opens_it()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var host = CreateHost(P(UIStrings.TitleDemoProject, UIStrings.HintDemoProject));
    host.Project = services.Project.Object;
    services.ProjectList
      .Setup(p => p.CreateAsync(UIStrings.TitleDemoProject, UIStrings.HintDemoProject, It.IsAny<CancellationToken>()))
      .ReturnsAsync(host);
    var imported = string.Empty;
    services.Importer
      .Setup(i => i.ImportAsync(It.IsAny<IProjectDocument>(), It.IsAny<TextReader>(), It.IsAny<CancellationToken>(), null))
      .Returns((IProjectDocument _, TextReader reader, CancellationToken _, string? _) =>
      {
        imported = reader.ReadToEnd();
        return Task.CompletedTask;
      });

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("Demo"));
    await commandTask;

    // The asset is read out of the app package, so this also pins its logical name and its encoding.
    Assert.Contains("1 NAME Charlotte /Brontë/", imported);
    services.CurrentProjectProvider.Verify(
      p => p.OpenAsync(It.IsAny<ProjectInfo>(), It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(
      n => n.GoToAsync($"{typeof(ProjectPage).Namespace}/{typeof(ProjectPage).Name}"), Times.Once());
  }

  [Fact]
  public async Task Settings_navigates_to_SettingsPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(SettingsPage).Namespace}/{typeof(SettingsPage).Name}";

    await page.InvokePageCommandAsync("Settings");

    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }
}
