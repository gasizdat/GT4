using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
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
  public async Task OnNavigatedTo_does_not_reload_when_the_project_revision_is_unchanged()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.WaitForFirstLoadAsync();
    var callsBefore = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));

    await MainThread.InvokeOnMainThreadAsync(page.InvokeNavigatedTo);

    var callsAfter = services.PersonManager.Invocations.Count(i => i.Method.Name == nameof(IPersonManager.GetPersonInfosAsync));
    Assert.Equal(callsBefore, callsAfter);
  }
}
