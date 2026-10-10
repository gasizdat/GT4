using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Pages;
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

  private static async Task<bool[]> RowVisibilityAsync(StatisticsPage page, string fieldName) =>
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var layout = (PageLayout)page.Content;
      var grid = (Grid)((ScrollView)layout.Body).Content;
      var labels = grid.Children.OfType<Label>().ToArray();
      var row = Grid.GetRow(labels.Single(label => label.Text == fieldName));
      return labels.Where(label => Grid.GetRow(label) == row).Select(label => label.IsVisible).ToArray();
    });

  private static readonly string[] ProjectWideFields =
  [
    Resources.UIStrings.FieldStatTotalFamilies,
    Resources.UIStrings.FieldStatLargestFamily,
    Resources.UIStrings.FieldStatSingleMemberFamilies,
  ];

  [Fact]
  public async Task A_family_scope_counts_only_that_familys_members()
  {
    var services = new TestServices();
    var family = N(100, "Smith", NameType.FamilyName);
    var husband = P(1, BiologicalSex.Male, names: [family]);
    var wife = P(2, BiologicalSex.Female, names: [family]);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([husband, wife, P(3, BiologicalSex.Unknown)]);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(family, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([husband, wife]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var statistics = await page.ReloadStatisticsAsync(() => page.FamilyName = family);

    Assert.Equal(2, statistics.TotalPersons);
    Assert.Equal(1, statistics.MenCount);
    Assert.Equal(1, statistics.WomenCount);
    Assert.Equal(0, statistics.UnknownSexCount);
    Assert.Equal(string.Format(Resources.UIStrings.TitleFamilyStatisticsPage_1, "Smith"), page.PageTitle);
  }

  // A member's relatives count in full, even from other families: most marriages join two surnames.
  [Fact]
  public async Task A_family_scope_counts_a_marriage_into_another_family()
  {
    var services = new TestServices();
    var family = N(100, "Smith", NameType.FamilyName);
    var husband = P(1, BiologicalSex.Male, names: [family]);
    var wifeFromAnotherFamily = P(2, BiologicalSex.Female);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(family, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([husband]);
    services.Relatives
      .Setup(r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, Relative[]> { [husband.Id] = [new Relative(wifeFromAnotherFamily, RelationshipType.Spouse, null)] });
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var statistics = await page.ReloadStatisticsAsync(() => page.FamilyName = family);

    Assert.Equal(1, statistics.MarriageCount);
    Assert.Equal(0, statistics.IsolatedPersonCount);
  }

  [Fact]
  public async Task The_NoFamily_scope_counts_the_persons_without_a_family_name()
  {
    var services = new TestServices();
    var family = N(100, "Smith", NameType.FamilyName);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, names: [family]), P(2), P(3)]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var statistics = await page.ReloadStatisticsAsync(() => page.FamilyName = NoFamily.Name);

    Assert.Equal(2, statistics.TotalPersons);
    services.PersonManager.Verify(
      p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  // Shell delivers the scope only after InitializeComponent has already started a whole-project load.
  [Fact]
  public async Task A_whole_project_load_finishing_after_the_scope_arrived_is_discarded()
  {
    var services = new TestServices();
    var family = N(100, "Smith", NameType.FamilyName);
    var wholeProject = new TaskCompletionSource<PersonInfo[]>();
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .Returns(wholeProject.Task);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(family, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, names: [family])]);
    var page = await CreatePageAsync(services);
    await page.ReloadStatisticsAsync(() => page.FamilyName = family);
    services.PersonManager.Verify(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()), Times.Once());

    wholeProject.SetResult([P(1, names: [family]), P(2), P(3)]);

    await Poll.ConfirmNeverAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.Statistics.TotalPersons),
      total => total != 1,
      TimeSpan.FromMilliseconds(300),
      failureMessage: "The stale whole-project load overwrote the family's statistics.");
  }

  [Fact]
  public async Task A_family_scope_hides_the_rows_that_count_families()
  {
    var services = new TestServices();
    var family = N(100, "Smith", NameType.FamilyName);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    await page.ReloadStatisticsAsync(() => page.FamilyName = family);

    foreach (var field in ProjectWideFields)
    {
      Assert.All(await RowVisibilityAsync(page, field), Assert.False);
    }
    Assert.All(await RowVisibilityAsync(page, Resources.UIStrings.FieldStatTotalPersons), Assert.True);
  }

  [Fact]
  public async Task The_project_wide_page_shows_the_rows_that_count_families_and_never_queries_by_name()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    foreach (var field in ProjectWideFields)
    {
      Assert.All(await RowVisibilityAsync(page, field), Assert.True);
    }
    Assert.Equal(Resources.UIStrings.TitleStatisticsPage, page.PageTitle);
    services.PersonManager.Verify(
      p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Never());
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
