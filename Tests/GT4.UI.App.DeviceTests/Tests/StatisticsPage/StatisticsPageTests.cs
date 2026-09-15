using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI;
using GT4.UI.Abstraction;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers StatisticsPage's lazy Statistics load (against a mocked Core, same TestServices as the
/// other page tests) and its revision-driven reload on navigation.
/// </summary>
public class StatisticsPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, BiologicalSex sex = BiologicalSex.Unknown, Date? birthDate = null, Date? deathDate = null, Name[]? names = null) =>
    new(id, birthDate ?? Date.Create(null, null, null, DateStatus.Unknown), deathDate, sex, names ?? [], null);

  private static async Task<TestableStatisticsPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableStatisticsPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_reports_zero_counts_against_an_empty_project()
  {
    var page = await CreatePageAsync(new TestServices());
    await page.WaitForFirstLoadAsync();

    Assert.Equal("0", page.TotalPersonsText);
    Assert.Equal(Resources.UIStrings.StatValueNone, page.AverageLifespanText);
  }

  [Fact]
  public async Task Statistics_loads_and_computes_from_the_project()
  {
    var services = new TestServices();
    var family = N(100, "Smith", NameType.FamilyName);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        P(1, BiologicalSex.Male, names: [family]),
        P(2, BiologicalSex.Female, names: [family]),
        P(3, BiologicalSex.Unknown),
      ]);
    services.FamilyManager
      .Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync([new FamilyInfo(family, null)]);
    var page = await CreatePageAsync(services);

    var statistics = await page.WaitForFirstLoadAsync();

    Assert.Equal(3, statistics.TotalPersons);
    Assert.Equal(1, statistics.TotalFamilies);
    Assert.Equal(1, statistics.MenCount);
    Assert.Equal(1, statistics.WomenCount);
    Assert.Equal(1, statistics.UnknownSexCount);
    Assert.Equal("3", page.TotalPersonsText);
    Assert.Equal("Smith (2)", page.TopLargestFamiliesText);
  }

  [Fact]
  public async Task OldestLivingText_and_MostChildrenText_format_names_via_INameFormatter_not_DisplayName()
  {
    var services = new TestServices();
    var currentYear = Date.Now.Year;
    var firstName = N(1, "John", NameType.FirstName);
    var lastName = N(2, "Smith", NameType.LastName);
    // LastName is stored before FirstName: PersonInfo.DisplayName would naively join Names in
    // array order ("Smith John"), while INameFormatter's CommonPersonName template ("FF PP LL")
    // always renders first name before last name regardless of storage order. This divergence is
    // what makes the assertions below actually catch a regression back to .DisplayName.
    var person = P(1, birthDate: Date.Create(currentYear - 40, 1, 1, DateStatus.WellKnown), names: [lastName, firstName]);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([person]);
    services.Relatives
      .Setup(r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, Relative[]> { [person.Id] = [new Relative(P(2), RelationshipType.Child, null)] });
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var nameFormatter = services.Provider.GetRequiredService<INameFormatter>();
    var expectedName = nameFormatter.ToString(person, NameFormat.CommonPersonName);

    Assert.Equal(string.Format(Resources.UIStrings.StatValuePersonYears_2, expectedName, 40), page.OldestLivingText);
    Assert.Equal(string.Format(Resources.UIStrings.StatValuePersonChildren_2, expectedName, 1), page.MostChildrenText);
    Assert.DoesNotContain("Smith John", page.OldestLivingText);
  }

  // One birth against three: uniform data would hide a bar that ignored the busiest count.
  private static async Task<TestableStatisticsPage> CreatePageWithTwoDecadesAsync()
  {
    var services = new TestServices();
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        P(1, birthDate: Date.Create(900, 1, 1, DateStatus.WellKnown)),
        P(2, birthDate: Date.Create(1910, 1, 1, DateStatus.WellKnown)),
        P(3, birthDate: Date.Create(1911, 1, 1, DateStatus.WellKnown)),
        P(4, birthDate: Date.Create(1912, 1, 1, DateStatus.WellKnown)),
      ]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    return page;
  }

  [Fact]
  public async Task Each_birth_decade_gets_a_bar_measured_against_the_busiest_one()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var groups = page.BirthsByDecade;

    Assert.Equal(2, groups.Length);
    Assert.Equal("1", groups[0].Bars[0].Count);
    Assert.Equal("3", groups[1].Bars[0].Count);
    // Each bar is its own count in stars, so the bars are exactly proportional to one another.
    Assert.Equal(new GridLength(1, GridUnitType.Star), groups[0].Bars[0].BarRows[1].Height);
    Assert.Equal(new GridLength(3, GridUnitType.Star), groups[1].Bars[0].BarRows[1].Height);
  }

  // The busiest bar's row above it is empty (nothing left to reserve), while a lonelier bar's rows
  // still add up to the same total -- that equal total is what keeps bars comparable to one another.
  [Fact]
  public async Task The_busiest_bar_fills_its_column_and_others_stay_proportional_to_it()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var groups = page.BirthsByDecade;

    Assert.Equal(new GridLength(0, GridUnitType.Star), groups[1].Bars[0].BarRows[0].Height);
    var lonelyColumn = groups[0].Bars[0].BarRows[0].Height.Value + groups[0].Bars[0].BarRows[1].Height.Value;
    var busiestColumn = groups[1].Bars[0].BarRows[0].Height.Value + groups[1].Bars[0].BarRows[1].Height.Value;
    Assert.Equal(busiestColumn, lonelyColumn, 6);
  }

  // With one decade per group (below the label cap), each group's label still names its own decade.
  [Fact]
  public async Task Every_group_label_names_the_decade_it_starts_from()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var groups = page.BirthsByDecade;

    Assert.Equal(string.Format(Resources.UIStrings.StatValueDecade_1, 900), groups[0].Decade);
    Assert.Equal(string.Format(Resources.UIStrings.StatValueDecade_1, 1910), groups[1].Decade);
  }

  // A group's label and its own bars must share one column: laid out separately they drift as soon
  // as one column measures a different width than another.
  [Fact]
  public async Task Each_rendered_group_shows_its_own_label()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var labels = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<FlexLayout>("BirthsByDecadeChart");
      var columns = chart.Children.OfType<Grid>();
      return columns.Select(column => ((Label)column.Children[1]).Text).ToArray();
    });

    Assert.Equal(page.BirthsByDecade.Select(g => g.Decade), labels);
  }

  // The star heights only reach the screen through a bound Grid.RowDefinitions, which fails
  // silently into an evenly split column if the binding stops resolving.
  [Fact]
  public async Task The_rendered_chart_takes_its_bar_heights_from_the_item_it_shows()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var groups = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<FlexLayout>("BirthsByDecadeChart");
      return chart.Children.OfType<Grid>().ToArray();
    });

    Assert.Equal(2, groups.Length);
    var bars = (Grid)groups[0].Children[0];
    var barColumn = (Grid)bars.Children[0];
    var barBox = (Grid)barColumn.Children[0];
    Assert.Equal(2, barBox.RowDefinitions.Count);
    Assert.Equal(page.BirthsByDecade[0].Bars[0].BarRows[0].Height, barBox.RowDefinitions[0].Height);
    Assert.Equal(page.BirthsByDecade[0].Bars[0].BarRows[1].Height, barBox.RowDefinitions[1].Height);
  }

  // Structural presence in the tree isn't placement: a bar with a silently-unresolved Grid.Column
  // binding would still show up in Children, just stacked in column 0 with every other bar.
  [Fact]
  public async Task Every_bar_in_a_group_lands_in_its_own_column()
  {
    var services = new TestServices();
    var persons = Enumerable.Range(0, 12)
      .Select(i => P(i + 1, birthDate: Date.Create(1900 + (i * 10), 1, 1, DateStatus.WellKnown)))
      .ToArray();
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(persons);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var groups = page.BirthsByDecade;
    Assert.Equal(6, groups.Length);
    Assert.Equal(2, groups[0].Bars.Length);

    var columns = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<FlexLayout>("BirthsByDecadeChart");
      var firstGroup = (Grid)chart.Children[0];
      var bars = (Grid)firstGroup.Children[0];
      return bars.Children.Select(bar => Grid.GetColumn((Grid)bar)).ToArray();
    });

    Assert.Equal([0, 1], columns);
  }

  // Once there are more decades than the label cap, decades group together so each label's own
  // column is wide enough to hold it, rather than crowding the x-axis with one label per decade.
  [Fact]
  public async Task Decades_group_together_once_there_are_more_of_them_than_the_label_cap()
  {
    var services = new TestServices();
    var persons = Enumerable.Range(0, 10)
      .Select(i => P(i + 1, birthDate: Date.Create(1900 + (i * 10), 1, 1, DateStatus.WellKnown)))
      .ToArray();
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(persons);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var groups = page.BirthsByDecade;

    Assert.Equal(5, groups.Length);
    Assert.All(groups, g => Assert.Equal(2, g.Bars.Length));
  }

  // Nothing else in this row renders StatValueNone, so a missing empty view just leaves it blank.
  [Fact]
  public async Task An_empty_project_still_says_so_where_the_decade_bars_would_be()
  {
    var page = await CreatePageAsync(new TestServices());
    await page.WaitForFirstLoadAsync();

    var texts = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<FlexLayout>("BirthsByDecadeChart");
      var labels = chart.Children.OfType<Label>();
      return labels.Select(l => l.Text).ToArray();
    });

    Assert.Equal([Resources.UIStrings.StatValueNone], texts);
  }

  [Fact]
  public async Task OnNavigatedTo_reloads_when_the_project_revision_changed_since_the_last_load()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var callsBefore = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 42 });

    await page.ReloadStatisticsAsync(page.InvokeNavigatedTo);

    var callsAfter = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    Assert.True(callsAfter > callsBefore);
  }

  [Fact]
  public async Task OnNavigatedTo_does_not_reload_when_the_project_revision_is_unchanged()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var loadsBefore = page.CompletedLoads;

    await MainThread.InvokeOnMainThreadAsync(page.InvokeNavigatedTo);
    await Task.Delay(200);

    Assert.Equal(loadsBefore, page.CompletedLoads);
  }

  [Fact]
  public async Task A_reload_of_an_empty_project_never_shows_the_indicator()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var statistics = await page.WaitForFirstLoadAsync();
    Assert.Equal(ProjectStatistics.Empty, statistics);
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 42 });

    var isLoading = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.InvokeNavigatedTo();
      return page.Loading.IsLoading;
    });

    Assert.False(isLoading);
  }
}
