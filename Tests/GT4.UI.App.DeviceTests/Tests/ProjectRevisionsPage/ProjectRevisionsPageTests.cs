using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Items;
using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers ProjectRevisionsPage's PageCommand/DeleteRevisionCommand branches. Both commands are
/// anonymous SafeCommand lambdas with no state to await, so the real page is registered/resolved
/// as-is (no protected-seam subclass) and checked by polling the mock invocation count after
/// Execute, same as MainPage.
/// </summary>
public class ProjectRevisionsPageTests
{
  private static ProjectRevision R(DateTime dateTime) =>
    new(dateTime, new FileDescription(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, []), $"version-{dateTime:yyyyMMddHHmmss}.gt4", null));

  private static async Task<ProjectRevisionsPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<ProjectRevisionsPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.NotNull(page.DeleteRevisionCommand);
    Assert.Null(page.SelectedRevision);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, page.RestoreBtnName);
  }

  [Fact]
  public async Task Revisions_maps_the_providers_revisions()
  {
    var services = new TestServices();
    var revision = R(new DateTime(2020, 1, 1));
    services.CurrentProjectProvider.SetupGet(p => p.Revisions).Returns([revision]);
    var page = await CreatePageAsync(services);

    var revisions = page.Revisions.ToArray();

    Assert.Single(revisions);
    Assert.Equal(revision, revisions[0].Info);
  }

  [Fact]
  public async Task SelectedRevision_changes_the_restore_button_name()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var item = new ProjectRevisionItem(R(new DateTime(2020, 1, 1)));

    await MainThread.InvokeOnMainThreadAsync(() => page.SelectedRevision = item);

    Assert.NotEqual(Resources.UIStrings.BtnNameCancel, page.RestoreBtnName);
  }

  [Fact]
  public async Task Restore_with_a_selected_revision_restores_and_navigates()
  {
    var services = new TestServices();
    var revision = R(new DateTime(2020, 1, 1));
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.SelectedRevision = new ProjectRevisionItem(revision));

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("Restore"));

    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Restore did not navigate.");
    services.CurrentProjectProvider.Verify(p => p.RestoreRevisionAsync(revision, It.IsAny<CancellationToken>()), Times.Once());
    var expectedRoute = $"{typeof(ProjectPage).Namespace}/{typeof(ProjectPage).Name}";
    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  [Fact]
  public async Task Restore_without_a_selected_revision_still_navigates()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services); // SelectedRevision stays null

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("Restore"));

    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Restore did not navigate.");
    services.CurrentProjectProvider.Verify(
      p => p.RestoreRevisionAsync(It.IsAny<ProjectRevision>(), It.IsAny<CancellationToken>()), Times.Never());
    var expectedRoute = $"{typeof(ProjectPage).Namespace}/{typeof(ProjectPage).Name}";
    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  [Fact]
  public async Task DeleteRevisionCommand_removes_the_revision()
  {
    var services = new TestServices();
    var revision = R(new DateTime(2020, 1, 1));
    var page = await CreatePageAsync(services);
    var item = new ProjectRevisionItem(revision);

    await MainThread.InvokeOnMainThreadAsync(() => page.DeleteRevisionCommand.Execute(item));

    await Poll.UntilAsync(
      () => Task.FromResult(services.CurrentProjectProvider.Invocations.Count(i => i.Method.Name == nameof(ICurrentProjectProvider.RemoveRevisionAsync))),
      count => count > 0,
      timeoutMessage: "DeleteRevisionCommand did not remove the revision.");
    services.CurrentProjectProvider.Verify(p => p.RemoveRevisionAsync(revision, It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task DeleteRevisionCommand_ignores_a_non_revision_parameter()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await MainThread.InvokeOnMainThreadAsync(() => page.DeleteRevisionCommand.Execute(new object()));

    services.CurrentProjectProvider.Verify(
      p => p.RemoveRevisionAsync(It.IsAny<ProjectRevision>(), It.IsAny<CancellationToken>()), Times.Never());
  }
}
