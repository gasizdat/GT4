using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Components.Genealogy;
using GT4.UI.Pages;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Genealogy;
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

  private static readonly double SlotPitch = new FamilyTreeLayoutMetrics().SlotPitch;

  // The centre with the given children one row below it; with nothing pinned each child sits under it.
  private static FamilyTree SetupTree(TestServices services, PersonInfo center, params PersonInfo[] children)
  {
    var tree = new FamilyTree(
      center.Id,
      [new FamilyTreeNode(center, 0), .. children.Select(child => new FamilyTreeNode(child, -1))],
      [.. children.Select(child => FamilyTreeEdge.ParentChild(parentId: center.Id, childId: child.Id))]);
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(tree);
    return tree;
  }

  // Every build from here on waits until the test completes the returned source. Continuations run
  // asynchronously, so releasing it never runs the rest of the load inline on the releasing thread.
  private static TaskCompletionSource<FamilyTree> HoldBuilds(TestServices services)
  {
    var held = new TaskCompletionSource<FamilyTree>(TaskCreationOptions.RunContinuationsAsynchronously);
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .Returns(held.Task);
    return held;
  }

  // Found through the person its tap carries. Main thread only.
  private static View NodeView(TestableFamilyTreePage page, int personId) =>
    page
      .FindByName<AbsoluteLayout>("Nodes")
      .Children
      .Cast<View>()
      .Single(view => view.GestureRecognizers.OfType<TapGestureRecognizer>().Any(tap => tap.CommandParameter is Person person && person.Id == personId));

  private static IPanGestureController NodeDrag(View view) =>
    view.GestureRecognizers.OfType<PanGestureRecognizer>().Single();

  // Many more columns than any runner window is wide, so the canvas can scroll both ways.
  private static PersonInfo[] ManyChildren() => [.. Enumerable.Range(2, 30).Select(id => P(id, $"Child{id}"))];

  private static Task<double> ScrollXAsync(TestableFamilyTreePage page) =>
    MainThread.InvokeOnMainThreadAsync(() => page.FindByName<ScrollView>("Scroller").ScrollX);

  private static async Task<double> ScrollToAsync(TestableFamilyTreePage page, double x)
  {
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var scroller = page.FindByName<ScrollView>("Scroller");
      return scroller.ScrollToAsync(x, scroller.ScrollY, animated: false);
    });

    return await Poll.UntilAsync(() => ScrollXAsync(page), scrolled => Math.Abs(scrolled - x) < 1, timeoutMessage: "The viewport did not scroll.");
  }

  private static Task<double> NodeLeftAsync(TestableFamilyTreePage page, int personId) =>
    MainThread.InvokeOnMainThreadAsync(() =>
    {
      var view = NodeView(page, personId);
      return AbsoluteLayout.GetLayoutBounds(view).Left;
    });

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

  // The returned probe reads what the page last stored. Each read is a fresh copy, as the real store's is.
  private static Func<int[]> UseHiddenPersons(TestServices services, params int[] ids)
  {
    var stored = ids;
    services.HiddenPersonsStore.Setup(s => s.Get(It.IsAny<ProjectInfo>())).Returns(() => [.. stored]);
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

  // Main thread only.
  private static MenuFlyoutItem HideItem(TestableFamilyTreePage page, int personId)
  {
    var view = NodeView(page, personId);
    var flyout = (MenuFlyout)FlyoutBase.GetContextFlyout(view);
    return (MenuFlyoutItem)flyout.Single();
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
  public async Task A_centre_with_a_stored_arrangement_is_laid_out_and_titled_as_arranged()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 3 });
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var offset = await ChildOffsetAsync(page);
    Assert.Equal(3, offset, precision: 6);
    Assert.True(page.IsArranged);
    var expectedTitle = string.Format(UIStrings.TitleFamilyTreePageArranged_1, "Ivan");
    Assert.Equal(expectedTitle, page.PageTitle);
  }

  [Fact]
  public async Task A_centre_without_an_arrangement_is_titled_plainly()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));

    Assert.False(page.IsArranged);
    var expectedTitle = string.Format(UIStrings.TitleFamilyTreePage_1, "Ivan");
    Assert.Equal(expectedTitle, page.PageTitle);
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
    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(page.PageTitle),
      title => !title.Contains("Ivan"),
      TimeSpan.FromMilliseconds(200),
      "A tap while arranging re-centred the tree.");

    services.NavigationService.Verify(
      n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>()),
      Times.Never());
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(other, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task Only_an_arranging_tree_lets_a_node_other_than_the_centre_be_dragged()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    Task<(bool CenterDrags, bool ChildDrags)> DragsAsync() =>
      MainThread.InvokeOnMainThreadAsync(() =>
      {
        var centerView = NodeView(page, center.Id);
        var childView = NodeView(page, child.Id);
        return (
          centerView.GestureRecognizers.OfType<PanGestureRecognizer>().Any(),
          childView.GestureRecognizers.OfType<PanGestureRecognizer>().Any());
      });

    var atRest = await DragsAsync();
    Assert.Equal((false, false), atRest);
    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = true);
    var arranging = await DragsAsync();
    Assert.Equal((false, true), arranging);
    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = false);
    var stopped = await DragsAsync();
    Assert.Equal((false, false), stopped);
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
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(2, 2 * SlotPitch));

    services.ArrangementStore.Verify(
      s => s.Set(
        TestServices.SampleProjectInfo,
        center.Id,
        It.Is<IReadOnlyDictionary<int, double>>(pins => pins.Count == 1 && Math.Abs(pins[2] - 2) < 1e-6)),
      Times.Once());
    var offset = await ChildOffsetAsync(page);
    Assert.Equal(2, offset, precision: 6);
    Assert.True(page.IsArranged);
  }

  [Fact]
  public async Task A_node_dropped_onto_a_pinned_one_lands_a_whole_slot_clear_of_it()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    var sibling = P(3, "Oleg");
    SetupTree(services, center, child, sibling);
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 1 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var centerLeft = await NodeLeftAsync(page, center.Id);
    var freeLeft = await NodeLeftAsync(page, sibling.Id);
    var pinnedLeft = centerLeft + SlotPitch;

    // Released 0.3 of a slot right of the pinned child: the nearer clear side is one slot further right.
    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(sibling.Id, pinnedLeft + (0.3 * SlotPitch) - freeLeft));

    var droppedCenterLeft = await NodeLeftAsync(page, center.Id);
    var childLeft = await NodeLeftAsync(page, child.Id);
    var siblingLeft = await NodeLeftAsync(page, sibling.Id);
    Assert.Equal(1, (childLeft - droppedCenterLeft) / SlotPitch, precision: 6);
    Assert.Equal(2, (siblingLeft - droppedCenterLeft) / SlotPitch, precision: 6);
    services.ArrangementStore.Verify(
      s => s.Set(
        TestServices.SampleProjectInfo,
        center.Id,
        It.Is<IReadOnlyDictionary<int, double>>(pins => pins[2] == 1 && Math.Abs(pins[3] - 2) < 1e-6)),
      Times.Once());
  }

  [Fact]
  public async Task A_node_cleared_of_a_pin_stays_put_when_that_pin_moves_away()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    var sibling = P(3, "Oleg");
    SetupTree(services, center, child, sibling);
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 1 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var centerLeft = await NodeLeftAsync(page, center.Id);
    var siblingLeft = await NodeLeftAsync(page, sibling.Id);
    // Released 0.3 of a slot right of the pinned child, so it lands a slot further right.
    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(sibling.Id, centerLeft + (1.3 * SlotPitch) - siblingLeft));

    // From one slot right of the centre to two slots left of it.
    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(child.Id, -3 * SlotPitch));

    var movedCenterLeft = await NodeLeftAsync(page, center.Id);
    var siblingMovedLeft = await NodeLeftAsync(page, sibling.Id);
    Assert.Equal(2, (siblingMovedLeft - movedCenterLeft) / SlotPitch, precision: 6);
  }

  [Fact]
  public async Task A_pinned_node_dropped_onto_another_pin_moves_itself_not_the_other()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    var sibling = P(3, "Oleg");
    SetupTree(services, center, child, sibling);
    // The sibling is pinned before the child, so a drop stored uncleared would push the child aside.
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [3] = -1, [2] = 1 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    // From one slot left of the centre to 0.3 of a slot right of the child.
    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(sibling.Id, 2.3 * SlotPitch));

    var centerLeft = await NodeLeftAsync(page, center.Id);
    var childLeft = await NodeLeftAsync(page, child.Id);
    var siblingLeft = await NodeLeftAsync(page, sibling.Id);
    Assert.Equal(1, (childLeft - centerLeft) / SlotPitch, precision: 6);
    Assert.Equal(2, (siblingLeft - centerLeft) / SlotPitch, precision: 6);
  }

  [Fact]
  public async Task ResetArrangement_forgets_it_and_lays_the_tree_out_afresh()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 3 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("ResetArrangement"));

    services.ArrangementStore.Verify(s => s.Clear(TestServices.SampleProjectInfo, center.Id), Times.Once());
    // The lone child is back under its parent, not left where the arrangement had it.
    var offset = await ChildOffsetAsync(page);
    Assert.Equal(0, offset, precision: 6);
    Assert.False(page.IsArranged);
    var expectedTitle = string.Format(UIStrings.TitleFamilyTreePage_1, "Ivan");
    Assert.Equal(expectedTitle, page.PageTitle);
  }

  [Fact]
  public async Task A_drop_while_a_load_is_in_flight_is_put_back_and_not_saved()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    var tree = SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var held = HoldBuilds(services);

    // ZoomIn, not Refresh: Refresh empties the node cache, leaving no node to drag.
    var translation = await MainThread.InvokeOnMainThreadAsync(async () =>
    {
      await page.InvokePageCommandAsync("ZoomIn");
      var view = NodeView(page, child.Id);
      view.TranslationX = 2 * SlotPitch;
      page.InvokeDropNode(child.Id, 2 * SlotPitch);
      return view.TranslationX;
    });
    await WaitForLoadAsync(page, services, () => held.SetResult(tree));

    Assert.Equal(0, translation);
    Assert.False(page.IsArranged);
    services.ArrangementStore.Verify(
      s => s.Set(It.IsAny<ProjectInfo>(), It.IsAny<int>(), It.IsAny<IReadOnlyDictionary<int, double>>()),
      Times.Never());
  }

  [Fact]
  public async Task A_drag_of_a_few_pixels_is_put_back_and_not_saved()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var translation = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var view = NodeView(page, child.Id);
      view.TranslationX = 4;
      page.InvokeDropNode(child.Id, 4);
      return view.TranslationX;
    });

    Assert.Equal(0, translation);
    Assert.False(page.LoadInProgress);
    Assert.False(page.IsArranged);
    services.ArrangementStore.Verify(
      s => s.Set(It.IsAny<ProjectInfo>(), It.IsAny<int>(), It.IsAny<IReadOnlyDictionary<int, double>>()),
      Times.Never());
  }

  [Fact]
  public async Task A_drag_well_under_a_slot_still_pins_the_node()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(child.Id, 50));

    var offset = await ChildOffsetAsync(page);
    Assert.Equal(50 / SlotPitch, offset, precision: 6);
    Assert.True(page.IsArranged);
  }

  [Fact]
  public async Task Turning_arranging_off_mid_drag_puts_the_node_back()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var (midDrag, afterOff) = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.IsArranging = true;
      var view = NodeView(page, child.Id);
      var drag = NodeDrag(view);
      drag.SendPanStarted(view, 1);
      drag.SendPan(view, SlotPitch, 0, 1);
      var shifted = view.TranslationX;
      page.IsArranging = false;
      return (shifted, view.TranslationX);
    });

    Assert.Equal(SlotPitch, midDrag);
    Assert.Equal(0, afterOff);
  }

  [Fact]
  public async Task Two_overlapping_drags_each_drop_by_their_own_offset()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var first = P(2, "Petr");
    var second = P(3, "Oleg");
    SetupTree(services, center, first, second);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var centerLeft = await NodeLeftAsync(page, center.Id);
    var firstLeft = await NodeLeftAsync(page, first.Id);
    var expected = ((firstLeft - centerLeft) / SlotPitch) + 3;
    await MainThread.InvokeOnMainThreadAsync(() => page.IsArranging = true);

    await WaitForLoadAsync(page, services, () =>
    {
      var firstView = NodeView(page, first.Id);
      var secondView = NodeView(page, second.Id);
      var firstDrag = NodeDrag(firstView);
      var secondDrag = NodeDrag(secondView);
      firstDrag.SendPanStarted(firstView, 1);
      secondDrag.SendPanStarted(secondView, 2);
      firstDrag.SendPan(firstView, 3 * SlotPitch, 0, 1);
      secondDrag.SendPan(secondView, -0.5 * SlotPitch, 0, 2);
      firstDrag.SendPanCompleted(firstView, 1);
    });

    services.ArrangementStore.Verify(
      s => s.Set(
        TestServices.SampleProjectInfo,
        center.Id,
        It.Is<IReadOnlyDictionary<int, double>>(pins => pins.Count == 1 && Math.Abs(pins[2] - expected) < 1e-6)),
      Times.Once());
  }

  [Fact]
  public async Task ResetArrangement_is_held_off_while_a_load_is_in_flight()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    var tree = SetupTree(services, center, child);
    services.ArrangementStore
      .Setup(s => s.Get(TestServices.SampleProjectInfo, center.Id))
      .Returns(new Dictionary<int, double> { [2] = 3 });
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var canResetAtRest = page.CanResetArrangement;
    var held = HoldBuilds(services);

    var canResetWhileLoading = await MainThread.InvokeOnMainThreadAsync(async () =>
    {
      await page.InvokePageCommandAsync("ZoomIn");
      return page.CanResetArrangement;
    });
    await WaitForLoadAsync(page, services, () => held.SetResult(tree));

    Assert.True(canResetAtRest);
    Assert.False(canResetWhileLoading);
    Assert.True(page.CanResetArrangement);
  }

  [Fact]
  public async Task While_arranging_dragging_the_canvas_does_not_scroll_it()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var children = ManyChildren();
    SetupTree(services, center, children);
    var page = await CreatePageAsync(services);
    await using var window = await WindowHost.AttachAsync(page);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var start = await ScrollToAsync(page, 1000);

    void PanCanvas(int gestureId)
    {
      var canvas = page.FindByName<Grid>("Canvas");
      IPanGestureController pan = canvas.GestureRecognizers.OfType<PanGestureRecognizer>().Single();
      pan.SendPanStarted(canvas, gestureId);
      pan.SendPan(canvas, -100, 0, gestureId);
      pan.SendPanCompleted(canvas, gestureId);
    }

    // Not arranging, the same drag scrolls, so the check below is able to fail.
    await MainThread.InvokeOnMainThreadAsync(() => PanCanvas(1));
    var panned = await Poll.UntilAsync(() => ScrollXAsync(page), x => Math.Abs(x - (start + 100)) < 1, timeoutMessage: "A canvas drag did not scroll even when not arranging.");

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.IsArranging = true;
      PanCanvas(2);
    });

    await Poll.ConfirmNeverAsync(
      () => ScrollXAsync(page),
      x => Math.Abs(x - panned) >= 1,
      TimeSpan.FromMilliseconds(300),
      "A canvas drag scrolled while arranging.");
  }

  [Fact]
  public async Task A_dropped_node_stays_where_it_was_released_on_screen()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var children = ManyChildren();
    SetupTree(services, center, children);
    var page = await CreatePageAsync(services);
    await using var window = await WindowHost.AttachAsync(page);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    // Off the centre: re-centring would also keep a node beside the centre in place, so only an
    // off-centre viewport tells following the drop apart from re-centring.
    var scrollBefore = await ScrollToAsync(page, 700);
    var dropped = children[children.Length / 2];
    var leftBefore = await NodeLeftAsync(page, dropped.Id);
    var deltaX = 1.5 * SlotPitch;
    var released = leftBefore + deltaX - scrollBefore;

    await WaitForLoadAsync(page, services, () => page.InvokeDropNode(dropped.Id, deltaX));

    await Poll.UntilAsync(
      async () =>
      {
        var left = await NodeLeftAsync(page, dropped.Id);
        var scroll = await ScrollXAsync(page);
        return left - scroll;
      },
      onScreen => Math.Abs(onScreen - released) < 1,
      timeoutMessage: "The dropped node did not stay where it was released.");
  }

  [Fact]
  public async Task Read_only_mode_hides_arranging_and_marks_its_reset_as_editing()
  {
    var page = await CreatePageAsync(new TestServices());
    var layout = page.FindByName<PageLayout>("LayoutView");
    var arrange = page.FindByName<Switch>("ArrangeSwitch");
    var reset = layout.MenuItems.Single(item => "ResetArrangement".Equals(item.CommandParameter));
    var shownWhileEditable = await MainThread.InvokeOnMainThreadAsync(() => arrange.IsVisible);

    try
    {
      var shownWhileReadOnly = await MainThread.InvokeOnMainThreadAsync(() =>
      {
        layout.ReadOnlyMode.Apply("True");
        return arrange.IsVisible;
      });

      Assert.True(shownWhileEditable);
      Assert.False(shownWhileReadOnly);
      Assert.True(reset.EditingAction);
    }
    finally
    {
      // ReadOnlyMode is the app-wide singleton, shared with every later test.
      await MainThread.InvokeOnMainThreadAsync(() => layout.ReadOnlyMode.Apply("False"));
    }
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
    var child = P(2, "Petr");
    SetupTree(services, center, child);
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

    Assert.Equal(new[] { child.Id }, hidden());
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.Is<int[]>(ids => ids.SequenceEqual(new[] { child.Id })), It.IsAny<CancellationToken>()),
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

  [Fact]
  public async Task Read_only_mode_offers_no_hide_and_disables_the_hidden_count()
  {
    var services = new TestServices();
    UseHiddenPersons(services, 7);
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    var layout = page.FindByName<PageLayout>("LayoutView");
    // Before the load: read-only mode is never switched while a tree page is up.
    await MainThread.InvokeOnMainThreadAsync(() => layout.ReadOnlyMode.Apply("True"));

    try
    {
      await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

      var flyout = await MainThread.InvokeOnMainThreadAsync(() =>
      {
        var view = NodeView(page, child.Id);
        return FlyoutBase.GetContextFlyout(view);
      });
      Assert.Null(flyout);
      Assert.True(page.HasHiddenPersons);
      Assert.False(page.CanChangeHidden);
    }
    finally
    {
      // ReadOnlyMode is the app-wide singleton, shared with every later test.
      await MainThread.InvokeOnMainThreadAsync(() => layout.ReadOnlyMode.Apply("False"));
    }
  }

  [Fact]
  public async Task Hiding_is_held_off_while_a_load_is_in_flight()
  {
    var services = new TestServices();
    UseHiddenPersons(services);
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    var tree = SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var canChangeAtRest = page.CanChangeHidden;
    var held = HoldBuilds(services);

    // ZoomIn, not Refresh: Refresh empties the node cache, leaving no node to hide.
    var canChangeWhileLoading = await MainThread.InvokeOnMainThreadAsync(async () =>
    {
      await page.InvokePageCommandAsync("ZoomIn");
      var hide = HideItem(page, child.Id);
      hide.Command.Execute(hide.CommandParameter);
      return page.CanChangeHidden;
    });
    await WaitForLoadAsync(page, services, () => held.SetResult(tree));

    Assert.True(canChangeAtRest);
    Assert.False(canChangeWhileLoading);
    Assert.True(page.CanChangeHidden);
    services.HiddenPersonsStore.Verify(s => s.Set(It.IsAny<ProjectInfo>(), It.IsAny<IEnumerable<int>>()), Times.Never());
  }

  [Fact]
  public async Task Returning_takes_in_a_hide_made_on_a_tree_opened_from_here_and_keeps_it()
  {
    var services = new TestServices();
    var hidden = UseHiddenPersons(services);
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    // Stands in for a second tree page, opened through the centre's person page.
    services.HiddenPersonsStore.Object.Set(TestServices.SampleProjectInfo, [7]);

    await WaitForLoadAsync(page, services, page.InvokeNavigatedTo);
    await WaitForLoadAsync(page, services, () =>
    {
      var hide = HideItem(page, child.Id);
      hide.Command.Execute(hide.CommandParameter);
    });

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.Is<int[]>(ids => ids.SequenceEqual(new[] { 7 })), It.IsAny<CancellationToken>()),
      Times.Once());
    Assert.Equal(new[] { 7, child.Id }, hidden());
  }

  [Fact]
  public async Task Returning_to_an_unchanged_hidden_set_does_not_rebuild()
  {
    var services = new TestServices();
    UseHiddenPersons(services, 7);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = P(1, "Ivan"));
    var loadsBefore = page.CompletedLoads;

    await MainThread.InvokeOnMainThreadAsync(page.InvokeNavigatedTo);

    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(page.CompletedLoads),
      loads => loads != loadsBefore,
      TimeSpan.FromMilliseconds(200),
      "Returning to an unchanged hidden set rebuilt the tree.");
  }

#if WINDOWS
  [Fact]
  public async Task The_hide_entry_reaches_the_native_node()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var child = P(2, "Petr");
    SetupTree(services, center, child);
    var page = await CreatePageAsync(services);
    await using var window = await WindowHost.AttachAsync(page);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var node = await MainThread.InvokeOnMainThreadAsync(() => NodeView(page, child.Id));

    var flyout = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => ((IPlatformViewHandler?)node.Handler)?.PlatformView?.ContextFlyout),
      native => native is not null,
      timeoutMessage: "The relative's node never got a native context flyout.");

    Assert.IsType<Microsoft.UI.Xaml.Controls.MenuFlyout>(flyout);
  }
#endif
}
