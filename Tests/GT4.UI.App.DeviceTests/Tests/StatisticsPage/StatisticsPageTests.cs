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

  // One birth against three, under names of different lengths: uniform data would hide both a bar
  // that ignored the busiest count and a name column measured per row.
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

    var bars = page.BirthsByDecade;

    Assert.Equal(2, bars.Length);
    Assert.Equal("1", bars[0].Count);
    Assert.Equal("3", bars[1].Count);
    // Each bar is its own count in stars, so the bars are exactly proportional to one another.
    Assert.Equal(new GridLength(1, GridUnitType.Star), bars[0].BarColumns[0].Width);
    Assert.Equal(new GridLength(3, GridUnitType.Star), bars[1].BarColumns[0].Width);
  }

  // The count sits past the end of its bar, so a bar filling the row would leave it nowhere to go.
  [Fact]
  public async Task The_busiest_bar_still_leaves_room_for_its_own_count()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var bars = page.BirthsByDecade;

    var gutter = bars[1].BarColumns[1].Width.Value;
    Assert.True(gutter > 0, "The busiest bar filled its row, leaving nothing for the count beside it.");
    // Equally wide rows are what keep the bars comparable: twice the length, twice the births.
    var lonelyRow = bars[0].BarColumns[0].Width.Value + bars[0].BarColumns[1].Width.Value;
    var busiestRow = bars[1].BarColumns[0].Width.Value + gutter;
    Assert.Equal(busiestRow, lonelyRow, 6);
  }

  // A name and its bar must share one row: laid out separately they drift as soon as one measures taller.
  [Fact]
  public async Task Every_decade_name_sits_in_the_row_of_its_own_bar()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var names = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<VerticalStackLayout>("BirthsByDecadeChart");
      var rows = chart.Children.OfType<Grid>();
      return rows.Select(row => ((Label)row.Children[0]).Text).ToArray();
    });

    Assert.Equal(page.BirthsByDecade.Select(b => b.Decade), names);
  }

  // Every row's name width comes from this label; measured per row, each bar would start elsewhere.
  [Fact]
  public async Task The_ruler_label_spells_the_longest_decade_name()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var ruler = await MainThread.InvokeOnMainThreadAsync(() => page.FindByName<Label>("DecadeNameRuler").Text);

    Assert.Equal(page.WidestDecadeName, ruler);
    Assert.Equal(page.BirthsByDecade[1].Decade, ruler);
  }

  // The star widths only reach the screen through a bound Grid.ColumnDefinitions, which fails
  // silently into an evenly split row if the binding stops resolving.
  [Fact]
  public async Task The_rendered_chart_takes_its_column_widths_from_the_bar_it_shows()
  {
    var page = await CreatePageWithTwoDecadesAsync();

    var rows = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<VerticalStackLayout>("BirthsByDecadeChart");
      return chart.Children.OfType<Grid>().ToArray();
    });

    Assert.Equal(2, rows.Length);
    var barRow = (Grid)rows[0].Children[1];
    Assert.Equal(2, barRow.ColumnDefinitions.Count);
    Assert.Equal(new GridLength(1, GridUnitType.Star), barRow.ColumnDefinitions[0].Width);
    Assert.Equal(page.BirthsByDecade[0].BarColumns[1].Width, barRow.ColumnDefinitions[1].Width);
  }

  // Nothing else in this row renders StatValueNone, so a missing empty view just leaves it blank.
  [Fact]
  public async Task An_empty_project_still_says_so_where_the_decade_bars_would_be()
  {
    var page = await CreatePageAsync(new TestServices());
    await page.WaitForFirstLoadAsync();

    var texts = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chart = page.FindByName<VerticalStackLayout>("BirthsByDecadeChart");
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
