using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI;
using GT4.UI.Abstraction;
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
      .ReturnsAsync([family]);
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

  [Fact]
  public async Task OnNavigatedTo_reloads_when_the_project_revision_changed_since_the_last_load()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var callsBefore = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    services.Project.SetupGet(p => p.ProjectRevision).Returns(42);

    await page.ReloadStatisticsAsync(page.InvokeNavigatedTo);

    var callsAfter = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    Assert.True(callsAfter > callsBefore);
  }

  [Fact]
  public async Task OnNavigatedTo_always_reloads_even_when_the_project_revision_is_unchanged()
  {
    // The whole point of #138: a commit landing after OnNavigatedTo samples the revision but before
    // the next navigation must not be missed, so navigation no longer gates the reload on a revision
    // compare at all -- it always refreshes.
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();

    var statistics = await page.ReloadStatisticsAsync(page.InvokeNavigatedTo);

    var callsAfter = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    Assert.True(callsAfter >= 2);
  }

  [Fact]
  public async Task RevisionChanged_reloads_while_the_page_is_loaded()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var monitor = (ProjectRevisionMonitor)services.Provider.GetRequiredService<IProjectRevisionMonitor>();
    await using var window = await WindowHost.AttachAsync(page);
    // Loaded (where the page subscribes to RevisionChanged) can fire after the native view merely
    // exists, so wait for the subscription itself before checking -- otherwise CheckRevision can land
    // before anyone is listening and the revision change is silently missed.
    await Poll.UntilAsync(() => Task.FromResult(monitor.SubscriberCount), count => count > 0, timeoutMessage: "The page never subscribed to RevisionChanged.");
    services.Project.SetupGet(p => p.ProjectRevision).Returns(42);

    await page.ReloadStatisticsAsync(monitor.CheckRevision);

    var callsAfter = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    Assert.True(callsAfter >= 2);
  }

  [Fact]
  public async Task RevisionChanged_does_not_reload_after_the_page_is_unloaded()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var monitor = (ProjectRevisionMonitor)services.Provider.GetRequiredService<IProjectRevisionMonitor>();
    var window = await WindowHost.AttachAsync(page);
    await Poll.UntilAsync(() => Task.FromResult(monitor.SubscriberCount), count => count > 0, timeoutMessage: "The page never subscribed to RevisionChanged.");
    await window.DisposeAsync();
    var callsBefore = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    services.Project.SetupGet(p => p.ProjectRevision).Returns(43);

    await MainThread.InvokeOnMainThreadAsync(monitor.CheckRevision);
    await Task.Delay(200);

    var callsAfter = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    Assert.Equal(callsBefore, callsAfter);
  }
}
