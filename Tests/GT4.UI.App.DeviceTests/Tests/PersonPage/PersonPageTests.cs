using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers PersonPage against a real MAUI runtime with a mocked Core. The relatives-tree rendering
/// itself is RelativeTree's own concern (see project_familytree/relatives-flat-tree memory), so
/// these tests assert scalar state (FullName, BirthDate, Photos) rather than Relatives rows.
/// PageCommand("EditPerson") pushes a modal via Navigation, driven through WindowHost/
/// ModalDialogHarness; CreateOrUpdatePersonDialog's own branches are covered by
/// CreateOrUpdatePersonDialogTests.
/// </summary>
public class PersonPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonFullInfo CreateSamplePerson() => PersonFullInfo.Empty with
  {
    Id = 1,
    BiologicalSex = BiologicalSex.Male,
    Names = [N(1, "Ivanov", NameType.FamilyName), N(2, "Ivan", NameType.FirstName | NameType.MaleDeclension)]
  };

  private static async Task<TestablePersonPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestablePersonPage>());
  }

  private static Task WaitForLoadAsync(TestablePersonPage page, TestServices services, Action interact) =>
    LoadWait.UntilAsync(() => page.CompletedLoads, services, interact, "Person");

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.False(page.ExpandAll);
    Assert.Empty(page.Photos);
  }

  [Fact]
  public async Task Loading_a_person_populates_scalar_state()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    Assert.Equal(person.Id, page.PersonFullInfo.Id);
    Assert.NotEmpty(page.Photos);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task MaritalStatusData_is_not_fetched_until_the_filter_panel_is_shown()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Never());

    await page.WaitForFilterDataAsync();
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task Loading_reports_non_teardown_exceptions_and_navigates_back()
  {
    var services = new TestServices();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("DB error"));
    var page = await CreatePageAsync(services);

    var loadsBefore = page.CompletedLoads;
    await MainThread.InvokeOnMainThreadAsync(() => page.PersonInfo = CreateSamplePerson());
    await Poll.UntilAsync(
      () => Task.FromResult(services.AlertService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Load failure was not reported.");

    Assert.Equal(loadsBefore, page.CompletedLoads);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Once());
    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Load failure did not navigate back.");
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task Loading_swallows_the_project_teardown_race()
  {
    var services = new TestServices();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ObjectDisposedException(nameof(IProjectDocument)));
    var page = await CreatePageAsync(services);

    var loadsBefore = page.CompletedLoads;
    await MainThread.InvokeOnMainThreadAsync(() => page.PersonInfo = CreateSamplePerson());
    // Nothing observable ever happens on the teardown path (no load, no alert, no navigation) -- poll
    // over a grace window and fail as soon as any of them fire, instead of a single check after a
    // blind delay that a regression could slip past if it fires later than the delay on a loaded runner.
    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(page.CompletedLoads != loadsBefore
        || services.AlertService.Invocations.Count > 0
        || services.NavigationService.Invocations.Count > 0),
      unwanted => unwanted,
      TimeSpan.FromMilliseconds(300),
      failureMessage: "The project-teardown race was not swallowed as expected.");

    Assert.Equal(loadsBefore, page.CompletedLoads);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
    services.NavigationService.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
  }

  [Fact]
  public async Task RemovePerson_removes_the_current_person()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await page.InvokePageCommandAsync("RemovePerson");

    services.Persons.Verify(p => p.RemovePersonAsync(person, It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task EditPerson_modal_updates_the_person_and_reloads()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditPerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.BirthDate = Date.Create(1990, 5, 1, DateStatus.WellKnown);
      dialog.DialogCommand.Execute("CreatePersonCommand");
    });
    await commandTask;

    services.PersonManager.Verify(p => p.UpdatePersonAsync(It.IsAny<PersonFullInfo>(), It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task EditPerson_does_not_duplicate_the_current_navigation_history_entry()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditPerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    await WaitForLoadAsync(page, services, () =>
    {
      dialog.BirthDate = Date.Create(1990, 5, 1, DateStatus.WellKnown);
      dialog.DialogCommand.Execute("CreatePersonCommand");
    });
    await commandTask;

    Assert.Equal(1, page.NavigationHistory.Count);
  }

  [Fact]
  public async Task EditPerson_cancelled_dialog_does_not_update()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditPerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("CreatePersonCommand"));
    await commandTask;

    services.PersonManager.Verify(p => p.UpdatePersonAsync(It.IsAny<PersonFullInfo>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task Wide_layout_keeps_the_photo_within_the_relatives_row_while_scrolling()
  {
    var services = new TestServices();
    var longBiography = new Data(
      Id: 0,
      Content: System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Line of biography text.\n", 80))),
      MimeType: System.Net.Mime.MediaTypeNames.Text.Plain,
      Category: default);
    var person = CreateSamplePerson() with { Biography = longBiography };
    var children = Enumerable.Range(2, 6)
      .Select(id => new RelativeInfo(
        id, Date.Create(2015, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male, [], null,
        RelationshipType.Child, null, Generation.Child, Consanguinity.Zero))
      .ToArray();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.RelativesProvider.Setup(r => r.GetChildren(It.IsAny<RelativeInfo[]>())).Returns(children);
    var page = await CreatePageAsync(services);
    await using var window = await WindowHost.AttachAsync(page);
    // Wide enough to land the landscape layout (photo left, relatives right, sharing one grid row).
    // Forced again after attaching/loading: attaching to the real window can trigger its own
    // OnSizeAllocated off the actual (narrower) test-runner window, overriding the forced one.
    await MainThread.InvokeOnMainThreadAsync(() => page.ForceSizeAllocated(2000, 800));
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    await MainThread.InvokeOnMainThreadAsync(() => page.ForceSizeAllocated(2000, 800));

    await Poll.UntilAsync(
      () => Task.FromResult(page.RelativesListForTest.Height),
      height => height > page.PersonPhotoForTest.Height,
      timeoutMessage: "The relatives row never grew taller than the photo.");
    var maxTranslation = page.RelativesListForTest.Height - page.PersonPhotoForTest.Height;

    await MainThread.InvokeOnMainThreadAsync(() => page.BodyScrollForTest.ScrollToAsync(0, 40, false));
    await Poll.UntilAsync(
      () => Task.FromResult(page.PersonPhotoForTest.TranslationY),
      translation => translation >= 40,
      timeoutMessage: "The photo did not track a scroll within its row's bounds.");
    Assert.Equal(40, page.PersonPhotoForTest.TranslationY);

    await MainThread.InvokeOnMainThreadAsync(() => page.BodyScrollForTest.ScrollToAsync(0, 100_000, false));
    await Poll.UntilAsync(
      () => Task.FromResult(page.PersonPhotoForTest.TranslationY),
      translation => translation >= maxTranslation,
      timeoutMessage: "The photo did not stop at the bottom of its row once scrolled past it.");
    Assert.Equal(maxTranslation, page.PersonPhotoForTest.TranslationY);
  }

  [Fact]
  public async Task GoToHome_navigates_to_MainPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(MainPage).Namespace}/{typeof(MainPage).Name}";

    await page.InvokePageCommandAsync("GoToHome");

    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  [Fact]
  public async Task GoToFamily_navigates_with_the_persons_family_name()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var expectedRoute = $"{typeof(FamilyPage).Namespace}/{typeof(FamilyPage).Name}";
    var expectedFamilyName = person.Names.Single(n => n.Type == NameType.FamilyName);

    await page.InvokePageCommandAsync("GoToFamily");

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => Equals(d["FamilyName"], expectedFamilyName))),
      Times.Once());
  }

  [Fact]
  public async Task GoToFamilyTree_navigates_with_a_plain_PersonInfo()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var expectedRoute = $"{typeof(FamilyTreePage).Namespace}/{typeof(FamilyTreePage).Name}";

    await page.InvokePageCommandAsync("GoToFamilyTree");

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        // Shell requires the exact runtime type PersonInfo, not the PersonFullInfo subclass --
        // see PersonPage.xaml.cs's own comment on this GoToFamilyTree branch.
        It.Is<Dictionary<string, object>>(d => d["PersonInfo"].GetType() == typeof(PersonInfo))),
      Times.Once());
  }
}
