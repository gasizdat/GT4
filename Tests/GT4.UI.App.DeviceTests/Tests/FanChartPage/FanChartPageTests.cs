using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components.Genealogy;
using GT4.UI.Pages;
using Moq;
using Xunit;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers FanChartPage's loading, navigation and settings. The sector geometry and hit testing are
/// FanChartLayout's, covered in GT4.UI.View.Tests; a tap here lands through TapAt at a point read off
/// the rendered labels, since the gesture itself is native-only.
/// </summary>
public class FanChartPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex = BiologicalSex.Male) =>
    new(id, Date.Create(1900, null, null, DateStatus.MonthUnknown), null, sex,
      [N(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  private static async Task<TestableFanChartPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableFanChartPage>());
  }

  private static Task WaitForLoadAsync(TestableFanChartPage page, TestServices services, Action interact) =>
    LoadWait.UntilAsync(() => page.CompletedLoads, services, interact, "FanChart");

  private static void UseTree(TestServices services, FamilyTree tree) =>
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(tree);

  // A line of fathers exactly as deep as each build asks for, so every depth is reached.
  private static void UseEndlessFathers(TestServices services, PersonInfo center) =>
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person _, int generations, int _, bool _, int[] _, CancellationToken _) => Fathers(center, generations));

  private static FamilyTree Fathers(PersonInfo center, int generations)
  {
    PersonInfo[] line = [center, .. Enumerable.Range(1, generations).Select(generation => P(center.Id + generation, $"Father{generation}"))];
    var edges = line.Zip(line.Skip(1), (child, father) => FamilyTreeEdge.ParentChild(father.Id, child.Id));
    return new FamilyTree(center.Id, [.. line.Select((person, generation) => new FamilyTreeNode(person, generation))], [.. edges]);
  }

  private static void VerifyBuilds(TestServices services, int generations, Times times) =>
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(It.IsAny<Person>(), generations, 0, false, It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      times);

  // Main thread only.
  private static Point LabelCenter(TestableFanChartPage page, string name)
  {
    var label = page
      .FindByName<AbsoluteLayout>("Labels")
      .Children
      .OfType<Label>()
      .Single(label => label.Text.Contains(name));
    return AbsoluteLayout.GetLayoutBounds(label).Center;
  }

  // Main thread only.
  private static Point CenterOfChart(TestableFanChartPage page)
  {
    var view = page
      .FindByName<AbsoluteLayout>("Labels")
      .Children
      .OfType<FamilyTreeNodeView>()
      .Single();
    return AbsoluteLayout.GetLayoutBounds(view).Center;
  }

  // Main thread only.
  private static Path[] SegmentPaths(TestableFanChartPage page) =>
    [.. page.FindByName<AbsoluteLayout>("Segments").Children.OfType<Path>()];

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.False(page.LoadInProgress);
    Assert.False(page.CanLoadMore);
    Assert.Equal(new[] { "180°", "270°", "360°" }, page.SweepNames);
    Assert.Equal(2, page.SweepIndex);
    // Dropping the interface leaves the zoom commands working while App's gesture and hotkeys go to the font.
    Assert.IsAssignableFrom<IZoomablePage>(page);
  }

  [Fact]
  public async Task Setting_PersonInfo_builds_four_generations_of_ancestors_only()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
    Assert.Contains("Ivan", page.PageTitle);
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, 4, 0, false, It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task Tapping_an_ancestor_recenters_the_chart_on_them()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var tree = Fathers(center, 2);
    var father = tree.Nodes.Single(node => node.Id == 2).Person;
    UseTree(services, tree);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () =>
    {
      var point = LabelCenter(page, "Father1");
      page.InvokeTapAt(point);
    });

    Assert.Contains("Father1", page.PageTitle);
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(father, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task Tapping_the_centre_navigates_to_PersonPage()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var expectedRoute = $"{typeof(PersonPage).Namespace}/{typeof(PersonPage).Name}";

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var point = CenterOfChart(page);
      page.InvokeTapAt(point);
    });

    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Tapping the centre never navigated.");
    services.NavigationService.Verify(
      n => n.GoToAsync(expectedRoute, true, It.Is<Dictionary<string, object>>(d => ((PersonInfo)d["PersonInfo"]).Id == center.Id)),
      Times.Once());
  }

  [Fact]
  public async Task Tapping_outside_every_segment_does_nothing()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    // The canvas corner is margin, outside every ring.
    await MainThread.InvokeOnMainThreadAsync(() => page.InvokeTapAt(new Point(1, 1)));
    await Task.Delay(200);

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(It.IsAny<Person>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
    Assert.Empty(services.NavigationService.Invocations);
  }

  [Fact]
  public async Task OpenPerson_navigates_with_the_current_center()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var expectedRoute = $"{typeof(PersonPage).Namespace}/{typeof(PersonPage).Name}";

    await page.InvokePageCommandAsync("OpenPerson");

    services.NavigationService.Verify(
      n => n.GoToAsync(expectedRoute, true, It.Is<Dictionary<string, object>>(d => ReferenceEquals(d["PersonInfo"], center))),
      Times.Once());
  }

  [Fact]
  public async Task LoadMore_adds_two_generations_at_a_time_up_to_ten()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseEndlessFathers(services, center);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    foreach (var _ in Enumerable.Range(0, 3))
    {
      Assert.True(page.CanLoadMore);
      await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("LoadMore"));
    }

    Assert.False(page.CanLoadMore);
    VerifyBuilds(services, 4, Times.Once());
    VerifyBuilds(services, 6, Times.Once());
    VerifyBuilds(services, 8, Times.Once());
    VerifyBuilds(services, 10, Times.Once());
  }

  [Fact]
  public async Task LoadMore_is_not_offered_when_the_chart_falls_short_of_the_depth_asked_for()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 3));
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    Assert.False(page.CanLoadMore);
  }

  [Fact]
  public async Task Choosing_a_sweep_stores_it_and_rebuilds_the_chart()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await WaitForLoadAsync(page, services, () => page.SweepIndex = 0);

    Assert.Equal("180", services.FanChartSweepSetting.Object.Value);
    VerifyBuilds(services, 4, Times.Exactly(2));
  }

  // The picker writes the index it already shows straight back, on a native echo of its own update.
  [Fact]
  public async Task Choosing_the_sweep_already_shown_neither_stores_nor_rebuilds()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    services.FanChartSweepSetting.Invocations.Clear();

    await MainThread.InvokeOnMainThreadAsync(() => page.SweepIndex = 2);
    await Task.Delay(200);

    services.FanChartSweepSetting.VerifySet(s => s.Value = It.IsAny<string>(), Times.Never());
    VerifyBuilds(services, 4, Times.Once());
  }

  [Fact]
  public async Task Starts_at_the_stored_sweep()
  {
    var services = new TestServices();
    services.FanChartSweepSetting.Object.Value = "270";

    var page = await CreatePageAsync(services);

    Assert.Equal(1, page.SweepIndex);
  }

  // Slots 2 and 3 are the parents, 4 and 6 their fathers: the same man, so slot 6 is a repeat and slot
  // 12, his father again, is never drawn.
  [Fact]
  public async Task A_repeated_ancestor_is_drawn_dashed_with_nothing_behind_it()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var father = P(2, "Father");
    var mother = P(3, "Mother", BiologicalSex.Female);
    var grandfather = P(4, "Grandfather");
    var greatGrandfather = P(5, "Great");
    FamilyTreeNode[] nodes = [new(center, 0), new(father, 1), new(mother, 1), new(grandfather, 2), new(greatGrandfather, 3)];
    FamilyTreeEdge[] edges =
    [
      FamilyTreeEdge.ParentChild(father.Id, center.Id),
      FamilyTreeEdge.ParentChild(mother.Id, center.Id),
      FamilyTreeEdge.ParentChild(grandfather.Id, father.Id),
      FamilyTreeEdge.ParentChild(grandfather.Id, mother.Id),
      FamilyTreeEdge.ParentChild(greatGrandfather.Id, grandfather.Id),
    ];
    UseTree(services, new FamilyTree(center.Id, nodes, edges));
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var paths = await MainThread.InvokeOnMainThreadAsync(() => SegmentPaths(page));
    Assert.Equal(5, paths.Length);
    Assert.Single(paths, path => path.StrokeDashArray.Count != 0);
  }

  [Fact]
  public async Task OnNavigatedTo_rebuilds_without_a_person_hidden_from_a_tree_since()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    services.HiddenPersonsStore.Setup(s => s.Get(It.IsAny<ProjectInfo>())).Returns([2]);

    await WaitForLoadAsync(page, services, page.InvokeNavigatedTo);

    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(center, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.Is<int[]>(ids => ids.SequenceEqual(new[] { 2 })), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task OnNavigatedTo_rebuilds_the_chart_when_the_project_revision_changed()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var loadsBefore = page.CompletedLoads;
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 1 });

    await WaitForLoadAsync(page, services, page.InvokeNavigatedTo);

    Assert.True(page.CompletedLoads > loadsBefore);
  }

  [Fact]
  public async Task OnNavigatedTo_does_not_rebuild_when_nothing_changed()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var loadsBefore = page.CompletedLoads;

    await MainThread.InvokeOnMainThreadAsync(page.InvokeNavigatedTo);
    await Task.Delay(200);

    Assert.Equal(loadsBefore, page.CompletedLoads);
  }
}
