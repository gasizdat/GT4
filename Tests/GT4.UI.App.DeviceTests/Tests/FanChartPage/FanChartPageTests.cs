using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Abstraction;
using GT4.UI.Components.Genealogy;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers FanChartPage's loading, navigation, settings and hover. The sector geometry and hit testing
/// are FanChartLayout's, covered in GT4.UI.View.Tests; a tap or hover here lands through TapAt or
/// HoverAt at a point read off the rendered labels, since the gestures themselves are native-only.
/// </summary>
public class FanChartPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex = BiologicalSex.Male, int birthYear = 1900) =>
    new(id, Date.Create(birthYear, null, null, DateStatus.MonthUnknown), null, sex,
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

  private static FamilyTree WithFather(PersonInfo child, PersonInfo father) =>
    new(child.Id, [new(child, 0), new(father, 1)], [FamilyTreeEdge.ParentChild(father.Id, child.Id)]);

  private static FamilyTree WithParents(PersonInfo child, PersonInfo father, PersonInfo mother) =>
    new(child.Id, [new(child, 0), new(father, 1), new(mother, 1)],
      [FamilyTreeEdge.ParentChild(father.Id, child.Id), FamilyTreeEdge.ParentChild(mother.Id, child.Id)]);

  // A father the Hebrew calendar can't hold (before 1583) and a mother it can.
  private static FamilyTree WithOldFather(PersonInfo child) =>
    WithParents(child, P(2, "Old", birthYear: 1575), P(3, "Mother", BiologicalSex.Female));

  private static void VerifyBuilds(TestServices services, int generations, Times times) =>
    services.FamilyTreeProvider.Verify(
      f => f.BuildAsync(It.IsAny<Person>(), generations, 0, false, It.IsAny<int[]>(), It.IsAny<CancellationToken>()),
      times);

  // A segment's label is drawn in spans, so its Text stays empty.
  private static string TextOf(Label label) => label.FormattedText.ToString();

  // Main thread only.
  private static Label LabelOf(TestableFanChartPage page, string name) =>
    page
      .FindByName<AbsoluteLayout>("Labels")
      .Children
      .OfType<Label>()
      .Single(label => TextOf(label).Contains(name));

  // Main thread only.
  private static Span[] SpansOf(TestableFanChartPage page, string name)
  {
    var label = LabelOf(page, name);
    return [.. label.FormattedText.Spans];
  }

  // Main thread only.
  private static Point LabelCenter(TestableFanChartPage page, string name)
  {
    var label = LabelOf(page, name);
    return AbsoluteLayout.GetLayoutBounds(label).Center;
  }

  // Main thread only.
  private static (bool IsVisible, string[] Texts) CalendarMark(TestableFanChartPage page)
  {
    var mark = page.FindByName<Label>("CalendarMark");
    string[] texts = [.. mark.FormattedText.Spans.Select(span => span.Text)];
    return (mark.IsVisible, texts);
  }

  // Main thread only. The legend is the mark's last span, empty while no year falls back.
  private static (bool IsShown, Color Color) FallbackLegend(TestableFanChartPage page)
  {
    var mark = page.FindByName<Label>("CalendarMark");
    var legend = mark.FormattedText.Spans.Last();
    return (legend.Text.Length != 0, legend.TextColor);
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
  private static double CenterRingThickness(TestableFanChartPage page)
  {
    var view = page
      .FindByName<AbsoluteLayout>("Labels")
      .Children
      .OfType<FamilyTreeNodeView>()
      .Single();
    var stack = (VerticalStackLayout)view.Content;
    return ((Border)stack.Children[0]).StrokeThickness;
  }

  // Main thread only.
  private static Label[] FatherLabels(TestableFanChartPage page) =>
    [.. page
      .FindByName<AbsoluteLayout>("Labels")
      .Children
      .OfType<Label>()
      .Where(label => TextOf(label).StartsWith("Father"))];

  // Main thread only.
  private static string[] LabelTexts(TestableFanChartPage page) =>
    [.. page.FindByName<AbsoluteLayout>("Labels").Children.OfType<Label>().Select(TextOf)];

  // Main thread only.
  private static Path[] SegmentPaths(TestableFanChartPage page) =>
    [.. page.FindByName<AbsoluteLayout>("Segments").Children.OfType<Path>()];

  // Main thread only.
  private static (double Thickness, int ZIndex, Rect Bounds)[] SegmentStyles(TestableFanChartPage page) =>
    [.. SegmentPaths(page).Select(path => (path.StrokeThickness, path.ZIndex, AbsoluteLayout.GetLayoutBounds(path)))];

  // Main thread only.
  private static Path[] Outlines(TestableFanChartPage page) =>
    [.. page.FindByName<AbsoluteLayout>("Highlights").Children.OfType<Path>()];

  // Over the first label naming the person; a repeat has a label of its own.
  private static Task<Path[]> HoverAsync(TestableFanChartPage page, string name) =>
    MainThread.InvokeOnMainThreadAsync(() =>
    {
      var label = page
        .FindByName<AbsoluteLayout>("Labels")
        .Children
        .OfType<Label>()
        .First(label => TextOf(label).Contains(name));
      var bounds = AbsoluteLayout.GetLayoutBounds(label);
      page.InvokeHoverAt(bounds.Center);
      return Outlines(page);
    });

  // Slots 2 and 3 are the parents, 4 and 6 their fathers: the same man, so slot 6 is a repeat and slot
  // 12, his father again, is never drawn.
  private static FamilyTree CollapsedTree(PersonInfo center)
  {
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
    return new FamilyTree(center.Id, nodes, edges);
  }

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
  public async Task A_load_overtaken_by_a_later_one_is_never_drawn()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    var tree = Fathers(center, 2);
    var father = tree.Nodes.Single(node => node.Id == 2).Person;
    var grandfather = tree.Nodes.Single(node => node.Id == 3).Person;
    UseTree(services, tree);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var overtakenTree = WithFather(father, P(10, "Overtaken"));
    var latestTree = WithFather(grandfather, P(11, "Latest"));
    var fathersTree = new TaskCompletionSource<FamilyTree>(TaskCreationOptions.RunContinuationsAsynchronously);
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(father, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .Returns(fathersTree.Task);
    services.FamilyTreeProvider
      .Setup(f => f.BuildAsync(grandfather, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(latestTree);

    // The father's chart is still loading when the grandfather is tapped on the first one.
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var fatherPoint = LabelCenter(page, "Father1");
      var grandfatherPoint = LabelCenter(page, "Father2");
      page.InvokeTapAt(fatherPoint);
      page.InvokeTapAt(grandfatherPoint);
    });
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => LabelTexts(page)),
      texts => texts.Any(text => text.Contains("Latest")),
      timeoutMessage: "The grandfather's chart was never drawn.");
    await WaitForLoadAsync(page, services, () => fathersTree.SetResult(overtakenTree));

    var texts = await MainThread.InvokeOnMainThreadAsync(() => LabelTexts(page));
    Assert.Contains(texts, text => text.Contains("Latest"));
    Assert.DoesNotContain(texts, text => text.Contains("Overtaken"));
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
  public async Task Zooming_in_labels_deeper_ancestors_in_smaller_text()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseEndlessFathers(services, center);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    foreach (var _ in Enumerable.Range(0, 3))
      await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("LoadMore"));
    var unzoomed = await MainThread.InvokeOnMainThreadAsync(() => FatherLabels(page));

    foreach (var _ in Enumerable.Range(0, 6))
      await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("ZoomIn"));

    var zoomed = await MainThread.InvokeOnMainThreadAsync(() => FatherLabels(page));
    Assert.True(zoomed.Length > unzoomed.Length);
    var largest = zoomed.Max(label => label.FontSize);
    Assert.Contains(zoomed, label => label.FontSize < largest);
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

  [Theory]
  [InlineData(DisplayCalendar.Julian)]
  [InlineData(DisplayCalendar.Hebrew)]
  [InlineData(DisplayCalendar.FrenchRepublican)]
  public async Task Names_another_calendar_in_the_header(DisplayCalendar calendar)
  {
    var services = new TestServices();
    services.FanChartCalendarSetting.Object.Value = $"{calendar}";

    var page = await CreatePageAsync(services);

    var mark = await MainThread.InvokeOnMainThreadAsync(() => CalendarMark(page));
    var expected = services.Provider.GetRequiredService<IDateFormatter>().CalendarName(calendar);
    Assert.True(mark.IsVisible);
    Assert.Contains(expected, mark.Texts);
  }

  [Fact]
  public async Task Under_Gregorian_names_no_calendar_and_mutes_no_year()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, WithOldFather(center));
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var (mark, legend, spans) = await MainThread.InvokeOnMainThreadAsync(() =>
      (CalendarMark(page), FallbackLegend(page), SpansOf(page, "Old")));
    Assert.False(mark.IsVisible);
    Assert.False(legend.IsShown);
    Assert.Contains(spans, span => span.Text == "1575");
    Assert.All(spans, span => Assert.Null(span.TextColor));
  }

  [Fact]
  public async Task A_year_the_calendar_cannot_hold_stays_Gregorian_in_the_legends_colour()
  {
    var services = new TestServices();
    services.FanChartCalendarSetting.Object.Value = nameof(DisplayCalendar.Hebrew);
    var center = P(1, "Ivan");
    UseTree(services, WithOldFather(center));
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var (legend, father, mother) = await MainThread.InvokeOnMainThreadAsync(() =>
      (FallbackLegend(page), SpansOf(page, "Old"), SpansOf(page, "Mother")));
    Assert.True(legend.IsShown);
    var fallback = Assert.Single(father, span => span.Text == "1575");
    Assert.NotNull(fallback.TextColor);
    Assert.Equal(legend.Color, fallback.TextColor);
    // 1900 spans the Hebrew new year, so the converted year is approximate.
    var converted = Assert.Single(mother, span => span.Text == "~5660");
    Assert.Null(converted.TextColor);
  }

  [Fact]
  public async Task The_legend_follows_the_chart_drawn()
  {
    var services = new TestServices();
    services.FanChartCalendarSetting.Object.Value = nameof(DisplayCalendar.Hebrew);
    var center = P(1, "Ivan");
    UseTree(services, WithOldFather(center));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var before = await MainThread.InvokeOnMainThreadAsync(() => FallbackLegend(page).IsShown);
    UseTree(services, Fathers(center, 1));

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("Refresh"));

    var after = await MainThread.InvokeOnMainThreadAsync(() => FallbackLegend(page).IsShown);
    Assert.True(before);
    Assert.False(after);
  }

  [Fact]
  public async Task A_header_too_narrow_for_one_row_wraps_inside_its_width()
  {
    var services = new TestServices();
    services.FanChartCalendarSetting.Object.Value = nameof(DisplayCalendar.FrenchRepublican);
    var center = P(1, "Ivan");
    UseTree(services, WithOldFather(center));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    await using (await WindowHost.AttachAsync(page))
    {
      await Poll.UntilAsync(
        () => MainThread.InvokeOnMainThreadAsync(() => page.FindByName<Picker>("SweepPicker").IsLoaded),
        loaded => loaded,
        timeoutMessage: "The sweep picker never loaded.");

      var layout = await MainThread.InvokeOnMainThreadAsync(() =>
      {
        var mark = page.FindByName<Label>("CalendarMark");
        var picker = page.FindByName<Picker>("SweepPicker");
        var span = (View)picker.Parent;
        var header = (ICrossPlatformLayout)mark.Parent;
        var line = mark.Measure(double.PositiveInfinity, double.PositiveInfinity);
        // Wide enough for the span picker, too narrow for the calendar's caption, name and legend on one line.
        var width = span.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        // A plain Arrange only queues a native pass; the layout manager arranges the children now.
        var size = header.CrossPlatformMeasure(width, double.PositiveInfinity);
        header.CrossPlatformArrange(new Rect(0, 0, width, size.Height));
        return (Width: width, Line: line, Mark: mark.Frame, Span: span.Frame);
      });

      // FlexLayout arranges in single precision, so an edge can land a few ulps past its double neighbour.
      const double Slack = 1e-3;
      Assert.True(layout.Line.Width > layout.Width, $"line {layout.Line}, width {layout.Width}");
      Assert.True(layout.Mark.Height > layout.Line.Height, $"mark {layout.Mark}, line {layout.Line}");
      Assert.True(layout.Mark.Bottom <= layout.Span.Top + Slack, $"mark {layout.Mark}, span {layout.Span}");
      Assert.True(layout.Mark.Right <= layout.Width + Slack, $"mark {layout.Mark}, width {layout.Width}");
      Assert.True(layout.Span.Right <= layout.Width + Slack, $"span {layout.Span}, width {layout.Width}");
    }
  }

  [Fact]
  public async Task A_repeated_ancestor_is_drawn_dashed_with_nothing_behind_it()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, CollapsedTree(center));
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var paths = await MainThread.InvokeOnMainThreadAsync(() => SegmentPaths(page));
    Assert.Equal(5, paths.Length);
    Assert.Single(paths, path => path.StrokeDashArray.Count != 0);
  }

  [Fact]
  public async Task Hovering_an_ancestor_outlines_their_segment_until_the_pointer_moves_on()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 2));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var plain = await MainThread.InvokeOnMainThreadAsync(() => SegmentPaths(page).Max(path => path.StrokeThickness));

    var first = await HoverAsync(page, "Father1");
    var firstOutline = Assert.Single(first);
    Assert.True(firstOutline.StrokeThickness > plain);
    // A fill would tint the segment under it.
    var fill = Assert.IsType<SolidColorBrush>(firstOutline.Fill);
    Assert.Equal(Colors.Transparent, fill.Color);

    var second = await HoverAsync(page, "Father2");
    var secondOutline = Assert.Single(second);
    Assert.NotSame(firstOutline, secondOutline);

    var none = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.InvokeHoverAt(null);
      return Outlines(page);
    });
    Assert.Empty(none);
  }

  [Fact]
  public async Task Hovering_the_centre_thickens_their_ring_until_the_pointer_moves_on()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 1));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var (plain, hovered, left) = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var centerPoint = CenterOfChart(page);
      var fatherPoint = LabelCenter(page, "Father1");
      var before = CenterRingThickness(page);
      page.InvokeHoverAt(centerPoint);
      var over = CenterRingThickness(page);
      page.InvokeHoverAt(fatherPoint);
      return (before, over, CenterRingThickness(page));
    });

    Assert.True(hovered > plain);
    Assert.Equal(plain, left);
  }

  [Fact]
  public async Task Hovering_leaves_the_segments_as_drawn()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, CollapsedTree(center));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    var drawn = await MainThread.InvokeOnMainThreadAsync(() => SegmentStyles(page));

    await HoverAsync(page, "Grandfather");

    var hovered = await MainThread.InvokeOnMainThreadAsync(() => SegmentStyles(page));
    Assert.Equal(drawn, hovered);
  }

  [Fact]
  public async Task Hovering_a_repeated_ancestor_outlines_every_segment_they_hold()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, CollapsedTree(center));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);

    var outlines = await HoverAsync(page, "Grandfather");

    Assert.Equal(2, outlines.Length);
    var dashed = Assert.Single(outlines, path => path.StrokeDashArray.Count != 0);
    var repeat = await MainThread.InvokeOnMainThreadAsync(() => SegmentPaths(page).Single(path => path.StrokeDashArray.Count != 0));
    // In step with the segment's own dashes, which would otherwise show through the outline's gaps.
    Assert.Equal(repeat.StrokeDashArray[0] * repeat.StrokeThickness, dashed.StrokeDashArray[0] * dashed.StrokeThickness, 6);
  }

  // A rebuild clears the outlines, so moving over the same person again must draw them on the new chart.
  [Fact]
  public async Task Hovering_the_same_ancestor_after_a_reload_outlines_them_again()
  {
    var services = new TestServices();
    var center = P(1, "Ivan");
    UseTree(services, Fathers(center, 2));
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = center);
    await HoverAsync(page, "Father1");

    await WaitForLoadAsync(page, services, () => page.InvokePageCommandAsync("Refresh"));
    var afterReload = await MainThread.InvokeOnMainThreadAsync(() => Outlines(page));
    var hoveredAgain = await HoverAsync(page, "Father1");

    Assert.Empty(afterReload);
    Assert.Single(hoveredAgain);
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
