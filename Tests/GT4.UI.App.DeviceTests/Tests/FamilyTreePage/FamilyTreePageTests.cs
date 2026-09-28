using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Resources;
using GT4.UI.Utils;
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
      f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
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

  private static readonly double SlotPitch = new FamilyTreeLayoutMetrics().SlotPitch;

  // The centre with the given children one row below it; with nothing pinned each child sits under it.
  private static void SetupTree(TestServices services, PersonInfo center, params PersonInfo[] children) =>
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new FamilyTree(
        center.Id,
        [new FamilyTreeNode(center, 0), .. children.Select(child => new FamilyTreeNode(child, -1))],
        [.. children.Select(child => FamilyTreeEdge.ParentChild(parentId: center.Id, childId: child.Id))]));

  private static Task<Rect[]> NodeBoundsAsync(TestableFamilyTreePage page) =>
    MainThread.InvokeOnMainThreadAsync(() => page
      .FindByName<AbsoluteLayout>("Nodes")
      .Children
      .Select(child => AbsoluteLayout.GetLayoutBounds((BindableObject)child))
      .ToArray());

  // With one centre and one child, how far the child sits from the centre, in slots.
  private static async Task<double> ChildOffsetAsync(TestableFamilyTreePage page)
  {
    var bounds = await NodeBoundsAsync(page);
    var center = bounds.MinBy(b => b.Top);
    var child = bounds.MaxBy(b => b.Top);
    return (child.Left - center.Left) / SlotPitch;
  }

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
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
  public async Task A_centre_with_a_stored_arrangement_is_laid_out_and_titled_as_arranged()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    SetupTree(services, center, P(2, "Petr"));
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 3 });
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    Assert.Equal(3, await ChildOffsetAsync(page), precision: 6);
    Assert.True(page.IsArranged);
    Assert.Equal(string.Format(UIStrings.TitleFamilyTreePageArranged_1, "Ivan"), page.PageTitle);
  }

  [Fact]
  public async Task A_centre_without_an_arrangement_is_titled_plainly()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));

    Assert.False(page.IsArranged);
    Assert.Equal(string.Format(UIStrings.TitleFamilyTreePage_1, "Ivan"), page.PageTitle);
  }

  [Fact]
  public async Task While_arranging_a_node_tap_neither_recenters_nor_navigates()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var center = P(1, "Ivan");
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var other = P(2, "Petr");
    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = true);

    await MainThread.InvokeOnMainThreadAsync(() => page.InvokePageCommandAsync(center));
    await MainThread.InvokeOnMainThreadAsync(() => page.InvokePageCommandAsync(other));
    await Task.Delay(200);

    services.NavigationService.Verify(
      n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>()),
      Times.Never());
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(other, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Never());
    Assert.Contains("Ivan", page.PageTitle);
  }

  [Fact]
  public async Task Only_an_arranging_tree_lets_a_node_other_than_the_centre_be_dragged()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    SetupTree(services, center, P(2, "Petr"));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    // The centre is the node in the top row.
    Task<(bool CenterDrags, bool ChildDrags)> DragsAsync() =>
      MainThread.InvokeOnMainThreadAsync(() =>
      {
        var views = page.FindByName<AbsoluteLayout>("Nodes").Children.Cast<View>().ToArray();
        var centerView = views.MinBy(v => AbsoluteLayout.GetLayoutBounds(v).Top)!;
        var childView = views.Single(v => v != centerView);
        return (
          centerView.GestureRecognizers.OfType<PanGestureRecognizer>().Any(),
          childView.GestureRecognizers.OfType<PanGestureRecognizer>().Any());
      });

    Assert.Equal((false, false), await DragsAsync());
    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = true);
    Assert.Equal((false, true), await DragsAsync());
    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = false);
    Assert.Equal((false, false), await DragsAsync());
  }

  [Fact]
  public async Task Arranging_swaps_the_hint_for_the_drag_one()
  {
    var page = await CreatePageAsync(new TestServices());

    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = true);

    Assert.Equal(UIStrings.HintFamilyTreePageArranging, page.PageHint);
  }

  [Fact]
  public async Task Dropping_a_node_pins_it_where_it_was_released_and_saves_it()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    SetupTree(services, center, P(2, "Petr"));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(2, 2 * SlotPitch));

    services.ArrangementStore.Verify(
      s => s.Set(
        TestServices.SampleProjectInfo,
        center.Id,
        It.Is<IReadOnlyDictionary<int, double>>(pins => pins.Count == 1 && Math.Abs(pins[2] - 2) < 1e-6)),
      Times.Once());
    Assert.Equal(2, await ChildOffsetAsync(page), precision: 6);
    Assert.True(page.IsArranged);
  }

  [Fact]
  public async Task A_node_dropped_onto_a_pinned_one_lands_a_whole_slot_clear_of_it()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    SetupTree(services, center, P(2, "Petr"), P(3, "Oleg"));
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 1 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var bounds = await NodeBoundsAsync(page);
    var centerLeft = bounds.MinBy(b => b.Top).Left;
    var children = bounds.Where(b => b.Top > bounds.Min(c => c.Top)).ToArray();
    var pinnedLeft = centerLeft + SlotPitch;
    var freeLeft = children.Single(b => Math.Abs(b.Left - pinnedLeft) > 1e-6).Left;

    // Released 0.3 of a slot right of the pinned child: the nearer clear side is one slot further right.
    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(3, pinnedLeft + (0.3 * SlotPitch) - freeLeft));

    services.ArrangementStore.Verify(
      s => s.Set(
        TestServices.SampleProjectInfo,
        center.Id,
        It.Is<IReadOnlyDictionary<int, double>>(pins => pins[2] == 1 && Math.Abs(pins[3] - 2) < 1e-6)),
      Times.Once());
  }

  [Fact]
  public async Task ResetArrangement_forgets_it_and_lays_the_tree_out_afresh()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    SetupTree(services, center, P(2, "Petr"));
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 3 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("ResetArrangement"));

    services.ArrangementStore.Verify(s => s.Clear(TestServices.SampleProjectInfo, center.Id), Times.Once());
    // The lone child is back under its parent, not left where the arrangement had it.
    Assert.Equal(0, await ChildOffsetAsync(page), precision: 6);
    Assert.False(page.IsArranged);
    Assert.Equal(string.Format(UIStrings.TitleFamilyTreePage_1, "Ivan"), page.PageTitle);
  }
}
