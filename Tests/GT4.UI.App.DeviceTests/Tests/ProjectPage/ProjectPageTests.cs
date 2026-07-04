using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers ProjectPage against a real MAUI runtime with a mocked Core. PageCommand("EditProject")
/// and PageCommand("CreateFamily") push modal dialogs via Navigation; those flows are driven through
/// WindowHost/ModalDialogHarness. ExportGedcom/ImportGedcom are out of scope -- they depend on
/// FileSystem/Share/FilePicker platform APIs, not just Core.
/// </summary>
public class ProjectPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static async Task<TestableProjectPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableProjectPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.Empty(page.Families);
  }

  [Fact]
  public async Task Families_sorts_with_the_registered_comparer()
  {
    var services = new TestServices();
    var unsorted = new[] { N(1, "Pushkin", NameType.FamilyName), N(2, "Aksakov", NameType.FamilyName), N(3, "Tolstoy", NameType.FamilyName) };
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(unsorted);
    var page = await CreatePageAsync(services);

    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());

    Assert.Equal(["Aksakov", "Pushkin", "Tolstoy"], families.Select(f => f.Info.Value));
  }

  [Fact]
  public async Task Families_reports_non_teardown_exceptions_via_AlertService()
  {
    var services = new TestServices();
    services.FamilyManager
      .Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("DB error"));
    var page = await CreatePageAsync(services);

    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());

    Assert.Empty(families);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Families_swallows_the_project_teardown_race()
  {
    var services = new TestServices();
    services.FamilyManager
      .Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ObjectDisposedException(nameof(IProjectDocument)));
    var page = await CreatePageAsync(services);

    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());

    Assert.Empty(families);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task RemoveProject_confirmed_removes_and_navigates_back()
  {
    var services = new TestServices();
    services.AlertService.Setup(a => a.ShowConfirmationAsync(It.IsAny<string>())).ReturnsAsync(true);
    var page = await CreatePageAsync(services);

    await page.InvokePageCommandAsync("RemoveProject");

    services.ProjectList.Verify(p => p.RemoveAsync(TestServices.SampleProjectInfo.Origin, It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task RemoveProject_cancelled_still_navigates_back_without_removing()
  {
    var services = new TestServices(); // ShowConfirmationAsync defaults to false (unconfigured)
    var page = await CreatePageAsync(services);

    await page.InvokePageCommandAsync("RemoveProject");

    // Unlike FamilyPage's RemoveFamily, ProjectPage navigates back unconditionally -- only the
    // removal itself is gated on confirmation.
    services.ProjectList.Verify(p => p.RemoveAsync(It.IsAny<FileDescription>(), It.IsAny<CancellationToken>()), Times.Never());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task EditProject_modal_updates_metadata_and_navigates_back()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditProject"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateProjectDialog>(page);

    Assert.Equal(TestServices.SampleProjectInfo.Name, dialog.ProjectName);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.ProjectName = "Renamed Project";
      dialog.ProjectDescription = "New description";
      dialog.OnCreateProjectBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    services.Metadata.Verify(m => m.SetProjectNameAsync("Renamed Project", It.IsAny<CancellationToken>()), Times.Once());
    services.Metadata.Verify(m => m.SetProjectDescriptionAsync("New description", It.IsAny<CancellationToken>()), Times.Once());
    services.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task EditProject_cancelled_dialog_does_not_update_or_navigate()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditProject"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateProjectDialog>(page);

    // Clearing the name makes the dialog's own OnCreateProjectBtn produce Name == string.Empty,
    // which ProjectPage treats as cancelled.
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.ProjectName = "";
      dialog.OnCreateProjectBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    services.Metadata.Verify(m => m.SetProjectNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    services.NavigationService.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
  }

  [Fact]
  public async Task CreateFamily_modal_adds_the_family()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("CreateFamily"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Ivanov";
      dialog.MaleName = "Ivan";
      dialog.FemaleName = "Ivanova";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync("Ivanov", "Ivan", "Ivanova", It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task CreateFamily_cancelled_dialog_does_not_add_a_family()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("CreateFamily"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty));
    await commandTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task GoToNames_navigates_to_NamesPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(NamesPage).Namespace}/{typeof(NamesPage).Name}";

    await page.InvokePageCommandAsync("GoToNames");

    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  [Fact]
  public async Task GoToRevisions_navigates_to_ProjectRevisionsPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(ProjectRevisionsPage).Namespace}/{typeof(ProjectRevisionsPage).Name}";

    await page.InvokePageCommandAsync("GoToRevisions");

    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  // OnFamilySelected is not covered: SelectionChangedEventArgs has no accessible constructor (both
  // are non-public, meant to be raised only by CollectionView itself), so it can't be constructed
  // from test code. The handler is a thin "take the selected item, navigate" wrapper with no other
  // logic worth pinning.
}
