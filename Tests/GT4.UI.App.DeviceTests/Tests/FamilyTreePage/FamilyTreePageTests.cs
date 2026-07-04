using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers FamilyTreePage's navigation logic. The connector/node canvas rendering, layout geometry,
/// thumbnails, and zoom-viewport math are FamilyTreeLayout's/native-canvas concerns, not the page's
/// -- and this page has a known unresolved GPU-texture-limit crash on deep trees (see
/// project_familytree_layout_cycle memory), so every test here uses a minimal (empty) mocked tree,
/// nowhere near that limit.
/// </summary>
public class FamilyTreePageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName) =>
    new(id, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male,
      [N(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  private static async Task<TestableFamilyTreePage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableFamilyTreePage>());
  }

  private static Task WaitForLoadAsync(TestableFamilyTreePage page, TestServices services, Action interact) =>
    LoadWait.UntilAsync(() => page.CompletedLoads, services, interact, "FamilyTree");

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.False(page.LoadInProgress);
    Assert.False(page.CanLoadMoreAncestors);
    Assert.False(page.CanLoadMoreDescendants);
  }

  [Fact]
  public async Task Setting_PersonInfo_completes_a_minimal_load()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
    Assert.Contains("Ivan", page.PageTitle);
  }

  [Fact]
  public async Task TappingTheCenterNode_navigates_to_PersonPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var expectedRoute = $"{typeof(PersonPage).Namespace}/{typeof(PersonPage).Name}";

    await page.InvokePageCommandAsync(center);

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => ReferenceEquals(d["PersonInfo"], center))),
      Times.Once());
  }

  [Fact]
  public async Task TappingADifferentNode_recenters_the_tree()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var other = P(2, "Petr");

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync(other));

    Assert.Contains("Petr", page.PageTitle);
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(other, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task OpenPerson_navigates_with_the_current_center()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var expectedRoute = $"{typeof(PersonPage).Namespace}/{typeof(PersonPage).Name}";

    await page.InvokePageCommandAsync("OpenPerson");

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => ReferenceEquals(d["PersonInfo"], center))),
      Times.Once());
  }

  [Fact]
  public async Task IncludeCollaterals_reloads_with_collaterals_included()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.IncludeCollaterals = true);

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task LoadAncestors_reloads_with_more_ancestor_generations()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("LoadAncestors"));

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, 5, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task LoadDescendants_reloads_with_more_descendant_generations()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("LoadDescendants"));

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), 5, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }
}
