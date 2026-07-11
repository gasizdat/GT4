using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Pages;
using Moq;
using Xunit;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers ProjectListPage against a real MAUI runtime with a mocked Core. PageCommand("Create")
/// pushes a modal CreateOrUpdateProjectDialog, driven through WindowHost/ModalDialogHarness.
/// OnProjectSelected is not covered (SelectionChangedEventArgs has no accessible constructor, same
/// as ProjectPage.OnFamilySelected); ImportGedcom is not covered (FilePicker is an external
/// dependency, same rationale as ProjectPage.ExportGedcom/ImportGedcom).
/// </summary>
public class ProjectListPageTests
{
  private static ProjectInfo P(string name, string description = "") => new(
    Name: name,
    Description: description,
    Revision: "",
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
      dialog.OnCreateProjectBtn(dialog, EventArgs.Empty);
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

    // Leaving ProjectName empty makes the dialog's own OnCreateProjectBtn produce Name ==
    // string.Empty, which ProjectListPage treats as cancelled.
    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnCreateProjectBtn(dialog, EventArgs.Empty));
    await commandTask;

    services.ProjectList.Verify(
      p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
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
