using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components.Genealogy;
using GT4.UI.Pages;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
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
  // Longer than the page's own pacing interval, so a step here is never refused for being too soon.
  private const int PastZoomInterval = 400;

  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  // Counting zoom steps has to go through the provider, not CompletedLoads: two loads that overlap
  // share one in-progress counter and so report a single completion between them.
  private static void VerifyBuilds(TestServices services, int expected) =>
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      Times.Exactly(expected));

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
    // Dropping the interface leaves every zoom test below green while App silently reverts to the font.
    Assert.IsAssignableFrom<IZoomablePage>(page);
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
  public async Task Only_the_build_over_an_empty_canvas_shows_the_indicator()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FamilyTree(center.Id, [new FamilyTreeNode(center, 0)], []));
    var page = await CreatePageAsync(services);

    var isLoadingOnFirstBuild = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.PersonInfo = center;
      return page.Loading.IsLoading;
    });
    Assert.True(isLoadingOnFirstBuild);
    await page.Loading.UntilIdleAsync("The indicator stayed on after the first build landed.");

    var isLoadingOnLoadMore = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      _ = page.InvokePageCommandAsync("LoadAncestors");
      return page.Loading.IsLoading;
    });

    Assert.False(isLoadingOnLoadMore);
  }

  [Fact]
  public async Task OnNavigatedTo_rebuilds_the_tree_when_the_project_revision_changed()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var loadsBefore = page.CompletedLoads;
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 1 });

    await WaitForLoadAsync(page, services, page.InvokeNavigatedTo);

    Assert.True(page.CompletedLoads > loadsBefore);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task OnNavigatedTo_does_not_rebuild_when_the_project_revision_is_unchanged()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var loadsBefore = page.CompletedLoads;

    await MainThread.InvokeOnMainThreadAsync(page.InvokeNavigatedTo);
    await Task.Delay(200);

    Assert.Equal(loadsBefore, page.CompletedLoads);
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
      f => f.BuildAsync(other, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
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
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
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
      f => f.BuildAsync(center, 5, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
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
      f => f.BuildAsync(center, It.IsAny<int>(), 5, It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task A_second_zoom_inside_the_pacing_interval_is_dropped()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () =>
    {
      page.Zoom(-FontScale.Step);
      page.Zoom(-FontScale.Step);
    });
    await Task.Delay(200);

    // The initial build, plus one step for the pair.
    VerifyBuilds(services, 2);
  }

  [Fact]
  public async Task A_zoom_after_the_pacing_interval_steps_again()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    await WaitForLoadAsync(page, services, () => page.Zoom(-FontScale.Step));

    await Task.Delay(PastZoomInterval);
    await WaitForLoadAsync(page, services, () => page.Zoom(-FontScale.Step));

    VerifyBuilds(services, 3);
  }

  [Fact]
  public async Task A_delta_far_below_a_whole_step_still_zooms()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var loadsBefore = page.CompletedLoads;

    await WaitForLoadAsync(page, services, () => page.Zoom(-0.001 * FontScale.Step));

    Assert.True(page.CompletedLoads > loadsBefore);
  }

  [Fact]
  public async Task Zooming_out_keeps_the_gap_that_clears_the_pinned_buttons()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FamilyTree(center.Id, [new FamilyTreeNode(center, 0)], []));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var canvas = page.FindByName<Grid>("Canvas");
    var heightAtFullZoom = canvas.HeightRequest;

    await Task.Delay(PastZoomInterval);
    await WaitForLoadAsync(page, services, () => page.Zoom(-FontScale.Step));

    // One step out is 0.75x and the canvas is one node between two gaps, so only the node may shrink.
    var nodeHeight = new FamilyTreeLayoutMetrics().NodeHeight;
    Assert.Equal(heightAtFullZoom - (0.25 * nodeHeight), canvas.HeightRequest, precision: 3);
  }

  [Fact]
  public async Task Zooming_out_past_the_minimum_stops_reloading()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    // 1.0 -> 0.75 -> 0.5 -> 0.4 (MinZoom, clamped); each of those is a real change of scale.
    for (var step = 0; step < 3; step++)
    {
      await Task.Delay(PastZoomInterval);
      await WaitForLoadAsync(page, services, () => page.Zoom(-FontScale.Step));
    }

    // Past the interval too, so the clamp is what refuses this one -- not the pacing.
    await Task.Delay(PastZoomInterval);
    await MainThread.InvokeOnMainThreadAsync(() => page.Zoom(-FontScale.Step));
    await Task.Delay(200);

    VerifyBuilds(services, 4);
  }

  // App reaches the zoom target via Shell.Current.CurrentPage, which must find a pushed page there.
  [Fact]
  public async Task A_pushed_tree_page_is_what_Shell_reports_as_the_current_page()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var shell = await MainThread.InvokeOnMainThreadAsync(
      () => new Shell { Items = { new ShellContent { Content = new ContentPage() } } });
    await using var attachment = await WindowHost.AttachAsync(shell);

    await MainThread.InvokeOnMainThreadAsync(() => shell.Navigation.PushAsync(page));

    var currentPage = await MainThread.InvokeOnMainThreadAsync(() => Shell.Current?.CurrentPage);
    Assert.Same(page, currentPage);
  }

  [Fact]
  public async Task ResetZoom_is_not_held_off_by_a_zoom_that_just_happened()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () =>
    {
      page.Zoom(-FontScale.Step);
      page.ResetZoom();
    });
    await Task.Delay(200);

    // The initial build, the step out, and the reset straight back -- the reset is not paced.
    VerifyBuilds(services, 3);
  }

  [Fact]
  public async Task ResetZoom_reloads_a_zoomed_tree_but_not_one_already_at_the_default()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("ZoomIn"));

    await WaitForLoadAsync(page, services, page.ResetZoom);
    var loadsAfterReset = page.CompletedLoads;
    await MainThread.InvokeOnMainThreadAsync(page.ResetZoom);
    await Task.Delay(200);

    Assert.Equal(loadsAfterReset, page.CompletedLoads);
  }

  [Fact]
  public async Task The_stored_hidden_persons_are_left_out_of_the_build_and_counted()
  {
    var services = new TestServices();
    UseHiddenPersons(services, 7);
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.Is<int[]>(ids => ids.SequenceEqual(new[] { 7 })), It.IsAny<CancellationToken>()),
      Times.Once());
    var expectedCount = string.Format(UIStrings.BtnNameHiddenPersons_1, 1);
    Assert.True(page.HasHiddenPersons);
    Assert.Equal(expectedCount, page.HiddenPersonsButtonName);
  }

  [Fact]
  public async Task Nothing_hidden_shows_no_count()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));

    Assert.False(page.HasHiddenPersons);
  }

  [Fact]
  public async Task Only_a_relative_offers_to_hide_and_hiding_rebuilds_without_them()
  {
    var services = new TestServices();
    var hidden = UseHiddenPersons(services);
    var center = P(1, "Ivan");
    var parent = P(2, "Petr");
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FamilyTree(center.Id, [new FamilyTreeNode(center, 0), new FamilyTreeNode(parent, 1)], [FamilyTreeEdge.ParentChild(parent.Id, center.Id)]));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var flyouts = await MainThread.InvokeOnMainThreadAsync(() => page
      .FindByName<AbsoluteLayout>("Nodes")
      .Children
      .OfType<FamilyTreeNodeView>()
      .Select(FlyoutBase.GetContextFlyout)
      .ToArray());
    Assert.Equal(2, flyouts.Length);
    var flyout = (MenuFlyout)Assert.Single(flyouts, candidate => candidate is not null);
    var hide = (MenuFlyoutItem)Assert.Single(flyout);
    Assert.Equal(UIStrings.MenuItemNameHideFromTree, hide.Text);

    await WaitForLoadAsync(page, services, () => hide.Command.Execute(hide.CommandParameter));

    Assert.Equal(new[] { parent.Id }, hidden());
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.Is<int[]>(ids => ids.SequenceEqual(new[] { parent.Id })), It.IsAny<CancellationToken>()),
      Times.Once());
    Assert.True(page.HasHiddenPersons);
  }

  [Fact]
  public async Task Unhiding_one_lists_only_existing_hidden_persons_and_keeps_the_rest_hidden()
  {
    var services = new TestServices();
    var petr = P(2, "Petr");
    var anna = P(3, "Anna");
    // 99 was hidden and has since been deleted from the project.
    var hidden = UseHiddenPersons(services, petr.Id, anna.Id, 99);
    UsePersons(services, P(1, "Ivan"), petr, anna);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));
    page.HiddenPersonAnswer = FullName(services, petr);

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("ShowHidden"));

    var expectedNames = new[] { FullName(services, anna), FullName(services, petr) };
    Assert.Equal(expectedNames, page.OfferedHiddenNames);
    Assert.Equal(new[] { anna.Id }, hidden());
  }

  [Fact]
  public async Task Show_all_empties_the_hidden_set()
  {
    var services = new TestServices();
    var petr = P(2, "Petr");
    var hidden = UseHiddenPersons(services, petr.Id);
    UsePersons(services, petr);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));
    page.HiddenPersonAnswer = UIStrings.BtnNameShowAll;

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("ShowHidden"));

    Assert.Empty(hidden());
    Assert.False(page.HasHiddenPersons);
  }

  [Fact]
  public async Task Cancelling_the_hidden_list_changes_nothing()
  {
    var services = new TestServices();
    var petr = P(2, "Petr");
    UseHiddenPersons(services, petr.Id);
    UsePersons(services, petr);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));

    await MainThread.InvokeOnMainThreadAsync(() => page.InvokePageCommandAsync("ShowHidden"));

    services.HiddenPersonsStore.Verify(s => s.Set(It.IsAny<ProjectInfo>(), It.IsAny<IEnumerable<int>>()), Times.Never());
  }

#if WINDOWS
  [Fact]
  public async Task The_hide_entry_reaches_the_native_node()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var parent = P(2, "Petr");
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FamilyTree(center.Id, [new FamilyTreeNode(center, 0), new FamilyTreeNode(parent, 1)], [FamilyTreeEdge.ParentChild(parent.Id, center.Id)]));
    var page = await CreatePageAsync(services);
    await using var window = await WindowHost.AttachAsync(page);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var node = await MainThread.InvokeOnMainThreadAsync(() => page
      .FindByName<AbsoluteLayout>("Nodes")
      .Children
      .OfType<FamilyTreeNodeView>()
      .Single(view => FlyoutBase.GetContextFlyout(view) is not null));

    var flyout = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => ((IPlatformViewHandler?)node.Handler)?.PlatformView?.ContextFlyout),
      native => native is not null,
      timeoutMessage: "The relative's node never got a native context flyout.");

    Assert.IsType<Microsoft.UI.Xaml.Controls.MenuFlyout>(flyout);
  }
#endif

  // The returned probe reads what the page last stored.
  private static Func<int[]> UseHiddenPersons(TestServices services, params int[] ids)
  {
    var stored = ids;
    services.HiddenPersonsStore.Setup(s => s.Get(It.IsAny<ProjectInfo>())).Returns(() => stored);
    services.HiddenPersonsStore
      .Setup(s => s.Set(It.IsAny<ProjectInfo>(), It.IsAny<IEnumerable<int>>()))
      .Callback((ProjectInfo _, IEnumerable<int> personIds) => stored = [.. personIds]);
    return () => stored;
  }

  private static void UsePersons(TestServices services, params PersonInfo[] persons)
  {
    services.Persons.Setup(p => p.GetPersonsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(persons);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(It.IsAny<Person[]>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person[] requested, bool _, CancellationToken _) => requested.Cast<PersonInfo>().ToArray());
  }

  private static string FullName(TestServices services, PersonInfo person) =>
    services.Provider.GetRequiredService<INameFormatter>().ToString(person, NameFormat.FullPersonName);
}
