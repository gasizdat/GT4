using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers DateCalendarPage's lazy CalendarMonth load (against a mocked Core, same TestServices as
/// the other page tests), its revision-driven reload on navigation, and its month/filter commands.
/// </summary>
public class DateCalendarPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, Date? birthDate = null, Date? deathDate = null, Name[]? names = null) =>
    new(id, birthDate ?? Date.Create(null, null, null, DateStatus.Unknown), deathDate, BiologicalSex.Unknown, names ?? [], null);

  private static async Task<TestableDateCalendarPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableDateCalendarPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_reports_an_empty_calendar_for_an_empty_project()
  {
    var page = await CreatePageAsync(new TestServices());
    await page.WaitForFirstLoadAsync();

    Assert.Empty(page.DayGroups);
    Assert.True(page.IsEmptyStateVisible);
  }

  [Fact]
  public async Task Loads_and_places_a_well_known_birthday_in_the_current_month()
  {
    var services = new TestServices();
    var currentYear = Date.Now.Year;
    var person = P(1, birthDate: Date.Create(currentYear - 40, Date.Now.Month, 1, DateStatus.WellKnown));
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([person]);
    var page = await CreatePageAsync(services);

    await page.WaitForFirstLoadAsync();

    var day = Assert.Single(page.DayGroups);
    Assert.Equal(1, day.Day);
    var entry = Assert.Single(day.Entries);
    Assert.Equal(DateCalendarEventType.Birth, entry.Type);
    Assert.Equal(person.Id, entry.Person.Id);
  }

  [Fact]
  public async Task Loads_a_photo_only_for_the_person_shown_in_the_displayed_month_not_the_whole_project()
  {
    var services = new TestServices();
    var currentYear = Date.Now.Year;
    var shown = P(1, birthDate: Date.Create(currentYear - 40, Date.Now.Month, 1, DateStatus.WellKnown));
    var elsewhere = P(2, birthDate: Date.Create(currentYear - 30, Date.Now.Month == 1 ? 2 : 1, 1, DateStatus.WellKnown));
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([shown, elsewhere]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    // The persons load and the month-scoped photo load each complete with their own RefreshView
    // call, so CompletedLoads (which counts CalendarMonth's OnPropertyChanged notifications) reaches
    // 2 once both have landed.
    await Poll.UntilAsync(
      () => Task.FromResult(page.CompletedLoads),
      loads => loads >= 2,
      TimeSpan.FromSeconds(10),
      "Month photo fetch did not complete.");

    services.PersonData.Verify(
      p => p.GetPersonDataSetAsync(
        It.Is<Person[]>(arr => arr.Length == 1 && arr[0].Id == shown.Id),
        It.IsAny<DataCategory?>(),
        It.IsAny<CancellationToken>()),
      Times.AtLeastOnce());
    services.PersonData.Verify(
      p => p.GetPersonDataSetAsync(
        It.Is<Person[]>(arr => arr.Any(x => x.Id == elsewhere.Id)),
        It.IsAny<DataCategory?>(),
        It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task PersonCommand_navigates_to_PersonPage_with_the_tapped_persons_own_PersonInfo()
  {
    var services = new TestServices();
    var currentYear = Date.Now.Year;
    var person = P(1, birthDate: Date.Create(currentYear - 40, Date.Now.Month, 1, DateStatus.WellKnown));
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([person]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var entry = Assert.Single(Assert.Single(page.DayGroups).Entries);
    var expectedRoute = $"{typeof(PersonPage).Namespace}/{typeof(PersonPage).Name}";

    await page.InvokeOpenPersonAsync(entry.Person);

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => ReferenceEquals(d["PersonInfo"], entry.Person))),
      Times.Once());
  }

  [Fact]
  public async Task Birthday_entry_for_a_deceased_person_uses_the_since_birth_phrasing_not_turns()
  {
    var services = new TestServices();
    var currentYear = Date.Now.Year;
    var birthDate = Date.Create(currentYear - 120, Date.Now.Month, 1, DateStatus.WellKnown);
    var deathDate = Date.Create(currentYear - 55, 1, 1, DateStatus.WellKnown);
    var person = P(1, birthDate: birthDate, deathDate: deathDate);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([person]);
    var page = await CreatePageAsync(services);

    await page.WaitForFirstLoadAsync();

    var nameFormatter = services.Provider.GetRequiredService<INameFormatter>();
    var expectedName = nameFormatter.ToString(person, NameFormat.CommonPersonName);
    var day = Assert.Single(page.DayGroups);
    var entry = Assert.Single(day.Entries);
    Assert.Equal(string.Format(UIStrings.DateCalendarBirthdayDeceasedText_2, expectedName, 120), entry.Text);
  }

  [Fact]
  public async Task Wedding_entry_formats_both_names_via_INameFormatter_not_DisplayName()
  {
    var services = new TestServices();
    var currentYear = Date.Now.Year;
    var lastNameA = N(1, "Smith", NameType.LastName);
    var firstNameA = N(2, "John", NameType.FirstName);
    // LastName stored before FirstName: DisplayName would naively join array order ("Smith John"),
    // while INameFormatter's CommonPersonName template always puts first name first -- same
    // divergence StatisticsPageTests relies on to catch a regression to .DisplayName.
    var personA = P(1, names: [lastNameA, firstNameA]);
    var personB = P(2, names: [N(3, "Jones", NameType.LastName), N(4, "Mary", NameType.FirstName)]);
    var weddingDate = Date.Create(currentYear - 25, Date.Now.Month, 1, DateStatus.WellKnown);

    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([personA, personB]);
    services.Relatives
      .Setup(r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, Relative[]>
      {
        [personA.Id] = [new Relative(personB, RelationshipType.Spouse, weddingDate)],
        [personB.Id] = [new Relative(personA, RelationshipType.Spouse, weddingDate)],
      });
    var page = await CreatePageAsync(services);

    await page.WaitForFirstLoadAsync();

    var nameFormatter = services.Provider.GetRequiredService<INameFormatter>();
    var expectedA = nameFormatter.ToString(personA, NameFormat.CommonPersonName);
    var expectedB = nameFormatter.ToString(personB, NameFormat.CommonPersonName);

    var day = Assert.Single(page.DayGroups);
    var entry = Assert.Single(day.Entries);
    Assert.Contains(expectedA, entry.Text);
    Assert.Contains(expectedB, entry.Text);
    Assert.True(entry.IsMilestone);
    Assert.DoesNotContain("Smith John", entry.Text);
  }

  [Fact]
  public async Task PrevMonth_command_moves_the_displayed_month_and_reveals_entries_from_it()
  {
    var services = new TestServices();
    var lastMonth = Date.Now.Month == 1 ? 12 : Date.Now.Month - 1;
    var person = P(1, birthDate: Date.Create(2000, lastMonth, 1, DateStatus.WellKnown));
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([person]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    Assert.Empty(page.DayGroups);

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("PrevMonth"));

    Assert.Single(page.DayGroups);
  }

  [Fact]
  public async Task ToggleBirths_command_hides_birth_entries_from_the_current_month()
  {
    var services = new TestServices();
    var person = P(1, birthDate: Date.Create(2000, Date.Now.Month, 1, DateStatus.WellKnown));
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([person]);
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    Assert.Single(page.DayGroups);
    Assert.True(page.IsBirthsActive);

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("ToggleBirths"));

    Assert.Empty(page.DayGroups);
    Assert.False(page.IsBirthsActive);
    // The active/inactive chip color is now a Style.Triggers DataTrigger on IsBirthsActive rather
    // than a page-computed Style, so it is not assertable from this headless harness -- verified
    // visually instead (screenshot before/after toggle on Windows).
  }

  [Fact]
  public async Task OnNavigatedTo_reloads_when_the_project_revision_changed_since_the_last_load()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var callsBefore = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 42 });

    await page.ReloadAsync(page.InvokeNavigatedTo);

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

  // Refresh resets _PersonsLoaded but leaves the previously loaded month rendered, so the load it
  // starts is a refresh over visible content -- not a first build.
  [Fact]
  public async Task A_reload_over_a_rendered_month_never_shows_the_indicator()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 42 });

    var isLoading = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.InvokeNavigatedTo();
      return page.Loading.IsLoading;
    });

    Assert.False(isLoading);
  }
}
