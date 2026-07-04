using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers MainPage's PageCommand navigation branches. SelectedLanguage is not covered: it mutates
/// the process-wide Utils.Language.Current and rebuilds a real AppShell, both too heavy/leaky for
/// this harness to touch safely; AppVersion/Languages are plain passthroughs with no branches.
/// </summary>
public class MainPageTests
{
  private static async Task<MainPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<MainPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
  }

  [Fact]
  public async Task OpenOrCreateDialog_navigates_to_ProjectListPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(ProjectListPage).Namespace}/{typeof(ProjectListPage).Name}";

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("OpenOrCreateDialog"));

    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "OpenOrCreateDialog did not navigate.");
    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  [Fact]
  public async Task OpenSettings_navigates_to_SettingsPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(SettingsPage).Namespace}/{typeof(SettingsPage).Name}";

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("OpenSettings"));

    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "OpenSettings did not navigate.");
    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }
}
