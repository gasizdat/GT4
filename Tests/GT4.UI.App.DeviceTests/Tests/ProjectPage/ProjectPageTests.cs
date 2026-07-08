using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers ProjectPage against a real MAUI runtime with a mocked Core. PageCommand("EditProject")
/// and PageCommand("CreateFamily") push modal dialogs via Navigation; those flows are driven through
/// WindowHost/ModalDialogHarness. ExportGedcom/ImportGedcom are out of scope -- they depend on
/// FileSystem/Share/FilePicker platform APIs, not just Core.
/// </summary>
public class ProjectPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static Date KnownYear(int year) => Date.Create(year, 1, 1, DateStatus.WellKnown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex = BiologicalSex.Male, Date? birthDate = null, Date? deathDate = null) =>
    new(id, birthDate ?? UnknownDate, deathDate, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  // EnsureFamiliesLoaded now groups persons by family in memory (matching PersonInfo.Names entries
  // against the family's Name.Id), mirroring the join GetPersonInfosByNameAsync performs against
  // PersonNames -- so a person mocked into a family must carry that family's Name in its Names array.
  private static PersonInfo InFamily(PersonInfo person, Name family) => person with { Names = [.. person.Names, family] };

  private static async Task<TestableProjectPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableProjectPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.Empty(page.Families);
    Assert.False(page.IsFiltersVisible);
  }

  [Fact]
  public async Task ToggleFilters_command_shows_and_hides_the_filters_panel()
  {
    var page = await CreatePageAsync(new TestServices());
    await using var window = await WindowHost.AttachAsync(page);
    Assert.False(page.IsFiltersVisible);

    // Setting IsFiltersVisible cascades into FadeVisibilityBehavior touching native UI, so this
    // must run on the UI thread; unlike the modal-dialog commands, there's nothing to drive in
    // between, so the started task is awaited right away.
    var openTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("ToggleFilters"));
    await openTask;
    Assert.True(page.IsFiltersVisible);

    var closeTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("ToggleFilters"));
    await closeTask;
    Assert.False(page.IsFiltersVisible);
  }

  [Fact]
  public async Task Families_sorts_with_the_registered_comparer()
  {
    var services = new TestServices();
    var unsorted = new[] { N(1, "Pushkin", NameType.FamilyName), N(2, "Aksakov", NameType.FamilyName), N(3, "Tolstoy", NameType.FamilyName) };
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(unsorted);
    var page = await CreatePageAsync(services);

    var families = await page.WaitForFamiliesAsync();

    Assert.Equal(["Aksakov", "Pushkin", "Tolstoy"], families.Select(f => f.Info.Value));
  }

  [Fact]
  public async Task Families_reports_non_teardown_exceptions_via_AlertService()
  {
    var services = new TestServices();
    services.FamilyManager
      .Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("DB error"));
    var page = await CreatePageAsync(services);

    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());

    // The background fetch throws asynchronously, so the alert can still be in flight once the
    // lines above return -- wait for it instead of racing SafeTask.Run's completion.
    await Poll.UntilAsync(
      () => Task.FromResult(services.AlertService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Load failure was not reported.");

    Assert.Empty(families);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Families_swallows_the_project_teardown_race()
  {
    var services = new TestServices();
    services.FamilyManager
      .Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ObjectDisposedException(nameof(IProjectDocument)));
    var page = await CreatePageAsync(services);

    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());

    // No positive signal to poll for here -- a swallowed exception leaves nothing observable.
    // Prove the negative over a window instead of a single check right after triggering it.
    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(services.AlertService.Invocations.Count),
      count => count > 0,
      TimeSpan.FromMilliseconds(300),
      failureMessage: "The project-teardown race was not swallowed as expected.");

    Assert.Empty(families);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task RemoveProject_confirmed_removes_and_navigates_back()
  {
    var services = new TestServices();
    services.AlertService.Setup(a => a.ShowConfirmationAsync(It.IsAny<string>())).ReturnsAsync(true);
    var page = await CreatePageAsync(services);

    await page.InvokePageCommandAsync("RemoveProject");

    services.ProjectList.Verify(p => p.RemoveAsync(TestServices.SampleProjectInfo.Origin, It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task RemoveProject_cancelled_still_navigates_back_without_removing()
  {
    var services = new TestServices(); // ShowConfirmationAsync defaults to false (unconfigured)
    var page = await CreatePageAsync(services);

    await page.InvokePageCommandAsync("RemoveProject");

    // Unlike FamilyPage's RemoveFamily, ProjectPage navigates back unconditionally -- only the
    // removal itself is gated on confirmation.
    services.ProjectList.Verify(p => p.RemoveAsync(It.IsAny<FileDescription>(), It.IsAny<CancellationToken>()), Times.Never());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task EditProject_modal_updates_metadata_and_navigates_back()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditProject"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateProjectDialog>(page);

    Assert.Equal(TestServices.SampleProjectInfo.Name, dialog.ProjectName);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.ProjectName = "Renamed Project";
      dialog.ProjectDescription = "New description";
      dialog.OnCreateProjectBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    services.Metadata.Verify(m => m.SetProjectNameAsync("Renamed Project", It.IsAny<CancellationToken>()), Times.Once());
    services.Metadata.Verify(m => m.SetProjectDescriptionAsync("New description", It.IsAny<CancellationToken>()), Times.Once());
    services.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task EditProject_cancelled_dialog_does_not_update_or_navigate()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditProject"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateProjectDialog>(page);

    // Clearing the name makes the dialog's own OnCreateProjectBtn produce Name == string.Empty,
    // which ProjectPage treats as cancelled.
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.ProjectName = "";
      dialog.OnCreateProjectBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    services.Metadata.Verify(m => m.SetProjectNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    services.NavigationService.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
  }

  [Fact]
  public async Task CreateFamily_modal_adds_the_family()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("CreateFamily"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Ivanov";
      dialog.MaleName = "Ivan";
      dialog.FemaleName = "Ivanova";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync("Ivanov", "Ivan", "Ivanova", It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task CreateFamily_cancelled_dialog_does_not_add_a_family()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("CreateFamily"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty));
    await commandTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task GoToNames_navigates_to_NamesPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(NamesPage).Namespace}/{typeof(NamesPage).Name}";

    await page.InvokePageCommandAsync("GoToNames");

    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  [Fact]
  public async Task GoToRevisions_navigates_to_ProjectRevisionsPage()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var expectedRoute = $"{typeof(ProjectRevisionsPage).Namespace}/{typeof(ProjectRevisionsPage).Name}";

    await page.InvokePageCommandAsync("GoToRevisions");

    services.NavigationService.Verify(n => n.GoToAsync(expectedRoute), Times.Once());
  }

  // OnFamilySelected is not covered: SelectionChangedEventArgs has no accessible constructor (both
  // are non-public, meant to be raised only by CollectionView itself), so it can't be constructed
  // from test code. The handler is a thin "take the selected item, navigate" wrapper with no other
  // logic worth pinning.

  [Fact]
  public async Task ToggleFilters_command_fades_the_real_filters_panel_in_and_out()
  {
    // The fade is not awaited by ToggleFilters: setting IsFiltersVisible raises PropertyChanged
    // synchronously, but FadeVisibilityBehavior's reaction to it is an async void animation that
    // keeps running after the property set returns -- so the command's own task completes well
    // before the 500ms fade does. Poll for the fade's own end state rather than assuming the
    // command's completion implies the animation's.
    var page = await CreatePageAsync(new TestServices());
    await using var window = await WindowHost.AttachAsync(page);

    var panel = await MainThread.InvokeOnMainThreadAsync(() => page.FindByName<Grid>("FiltersPanel"));
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      Assert.False(panel.IsVisible);
      Assert.Equal(0, panel.Opacity);
    });

    var openTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("ToggleFilters"));
    await openTask;
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => panel.Opacity),
      opacity => opacity == 1,
      timeoutMessage: "The filters panel did not finish fading in.");
    await MainThread.InvokeOnMainThreadAsync(() => Assert.True(panel.IsVisible));

    var closeTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("ToggleFilters"));
    await closeTask;
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => panel.IsVisible),
      isVisible => !isVisible,
      timeoutMessage: "The filters panel did not finish fading out.");
  }

  [Fact]
  public async Task ClearFiltersButton_fades_in_and_out_with_filter_activity_regardless_of_panel_visibility()
  {
    var page = await CreatePageAsync(new TestServices());
    await using var window = await WindowHost.AttachAsync(page);

    var clearButton = await MainThread.InvokeOnMainThreadAsync(() => page.FindByName<Button>("ClearFiltersButton"));
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      Assert.False(clearButton.IsVisible);
      Assert.Equal(0, clearButton.Opacity);
    });

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.NameFilter = "John");
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => clearButton.Opacity),
      opacity => opacity == 1,
      timeoutMessage: "The clear-filters button did not finish fading in.");
    await MainThread.InvokeOnMainThreadAsync(() => Assert.True(clearButton.IsVisible));

    // The panel was never opened in this test -- proves the button's visibility no longer
    // depends on IsFiltersVisible.
    await MainThread.InvokeOnMainThreadAsync(() => Assert.False(page.IsFiltersVisible));

    var clearTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("ClearFilters"));
    await clearTask;
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => clearButton.IsVisible),
      isVisible => !isVisible,
      timeoutMessage: "The clear-filters button did not finish fading out.");
  }

  [Fact]
  public async Task NameFilter_matches_any_name_part_with_wildcards()
  {
    var services = new TestServices();
    var ivanov = N(1, "Ivanov", NameType.FamilyName);
    var petrov = N(2, "Petrov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ivanov, petrov]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John"), ivanov), InFamily(P(2, "Jane"), ivanov), InFamily(P(3, "Mark"), petrov)]);
    var page = await CreatePageAsync(services);
    await page.WaitForFamiliesAsync();

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.NameFilter = "J*n");
    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());

    var family = Assert.Single(families);
    Assert.Equal("Ivanov", family.Info.Value);
    Assert.Equal(["John"], family.Persons.Select(p => p.DisplayName));
  }

  [Fact]
  public async Task NameFilter_cleared_shows_everyone_again()
  {
    var services = new TestServices();
    var ivanov = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ivanov]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John"), ivanov), InFamily(P(2, "Jane"), ivanov)]);
    var page = await CreatePageAsync(services);
    await page.WaitForFamiliesAsync();

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.NameFilter = "John");
    var filtered = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Single(filtered);

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.NameFilter = "");
    var restored = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(2, restored.Count);
  }

  [Fact]
  public async Task SexFilterIndex_filters_by_biological_sex()
  {
    var services = new TestServices();
    var family = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([family]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John", BiologicalSex.Male), family), InFamily(P(2, "Jane", BiologicalSex.Female), family)]);
    var page = await CreatePageAsync(services);
    await page.WaitForFamiliesAsync();

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.SexFilterIndex = 1); // Male
    var maleOnly = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(["John"], maleOnly.Select(p => p.DisplayName));

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.SexFilterIndex = 2); // Female
    var femaleOnly = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(["Jane"], femaleOnly.Select(p => p.DisplayName));

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.SexFilterIndex = 0); // Any
    var everyone = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(2, everyone.Count);
  }

  [Fact]
  public async Task MaritalStatusFilterIndex_filters_by_marital_status()
  {
    var services = new TestServices();
    var family = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([family]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John"), family), InFamily(P(2, "Jane"), family)]);
    services.Relatives
      .Setup(r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, Relative[]> { [1] = [new Relative(2, default, null, BiologicalSex.Female, RelationshipType.Spouse, null)] });
    var page = await CreatePageAsync(services);
    await page.WaitForFamiliesAsync();
    // Marital status is fetched lazily, the first time the filter panel is shown, not up front.
    await page.WaitForFilterDataAsync();

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.MaritalStatusFilterIndex = 1); // Married
    var married = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(["John"], married.Select(p => p.DisplayName));

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.MaritalStatusFilterIndex = 2); // Single
    var single = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(["Jane"], single.Select(p => p.DisplayName));
  }

  [Fact]
  public async Task MaritalStatusData_is_not_fetched_until_the_filter_panel_is_shown()
  {
    var services = new TestServices();
    var family = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([family]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John"), family)]);
    var page = await CreatePageAsync(services);

    await page.WaitForFamiliesAsync();
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Never());

    await page.WaitForFilterDataAsync();
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task YearFilter_matches_persons_alive_in_the_selected_year()
  {
    var services = new TestServices();
    var family = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([family]);
    var bornKnown = InFamily(P(1, "John", birthDate: KnownYear(1950), deathDate: KnownYear(2000)), family);
    var noDatesAtAll = InFamily(P(2, "NoDates"), family);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([bornKnown, noDatesAtAll]);
    var page = await CreatePageAsync(services);

    var loaded = await page.WaitForFamiliesAsync();
    var beforeFilter = loaded.Single().Persons;
    Assert.Equal(2, beforeFilter.Count);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.Filter.IsYearFilterEnabled = true;
      page.Filter.SelectedYear = 1975;
    });
    var withinRange = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(["John"], withinRange.Select(p => p.DisplayName));

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.SelectedYear = 2010);
    var outsideRange = await MainThread.InvokeOnMainThreadAsync(() => page.Families.ToArray());
    Assert.Empty(outsideRange);
  }

  [Fact]
  public async Task ClearFilters_resets_every_filter_and_restores_the_full_list()
  {
    var services = new TestServices();
    var family = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([family]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John", BiologicalSex.Male), family), InFamily(P(2, "Jane", BiologicalSex.Female), family)]);
    var page = await CreatePageAsync(services);
    await page.WaitForFamiliesAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.Filter.NameFilter = "John";
      page.Filter.SexFilterIndex = 1;
      page.Filter.MaritalStatusFilterIndex = 1;
      page.Filter.IsYearFilterEnabled = true;
    });

    await page.InvokePageCommandAsync("ClearFilters");

    var state = await MainThread.InvokeOnMainThreadAsync(() =>
      (page.Filter.NameFilter, page.Filter.SexFilterIndex, page.Filter.MaritalStatusFilterIndex, page.Filter.IsYearFilterEnabled));
    Assert.Equal((string.Empty, 0, 0, false), state);

    var everyone = await MainThread.InvokeOnMainThreadAsync(() => page.Families.Single().Persons);
    Assert.Equal(2, everyone.Count);
  }

  [Fact]
  public async Task IsAnyFilterActive_reflects_filter_state_independently_of_panel_visibility()
  {
    var page = await CreatePageAsync(new TestServices());
    Assert.False(page.IsAnyFilterActive);

    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.NameFilter = "John");
    Assert.True(page.IsAnyFilterActive);

    // Closing the panel must not hide the "a filter is active" signal.
    await MainThread.InvokeOnMainThreadAsync(() => page.IsFiltersVisible = false);
    Assert.True(page.IsAnyFilterActive);

    await page.InvokePageCommandAsync("ClearFilters");
    Assert.False(page.IsAnyFilterActive);
  }

  [Fact]
  public async Task Families_returns_the_same_collection_instance_across_filter_changes()
  {
    var services = new TestServices();
    var family = N(1, "Ivanov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([family]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([InFamily(P(1, "John"), family), InFamily(P(2, "Jane"), family)]);
    var page = await CreatePageAsync(services);

    var before = await MainThread.InvokeOnMainThreadAsync(() => page.Families);
    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.NameFilter = "John");
    var after = page.Families;

    Assert.Same(before, after);
  }

  [Fact]
  public async Task Filtering_leaves_an_unaffected_familys_persons_collection_untouched()
  {
    var services = new TestServices();
    var ivanov = N(1, "Ivanov", NameType.FamilyName);
    var petrov = N(2, "Petrov", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ivanov, petrov]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([
        InFamily(P(1, "John", BiologicalSex.Male), ivanov),
        InFamily(P(2, "Jane", BiologicalSex.Female), ivanov),
        InFamily(P(3, "Mark", BiologicalSex.Male), petrov)]);
    var page = await CreatePageAsync(services);
    await page.WaitForFamiliesAsync();

    var families = await MainThread.InvokeOnMainThreadAsync(() => page.Families);
    var petrovBefore = families.Single(f => f.Info.Value == "Petrov");
    var petrovPersonsBefore = petrovBefore.Persons;
    var petrovChangeCount = 0;
    petrovPersonsBefore.CollectionChanged += (_, _) => petrovChangeCount++;

    // Petrov's one member (Mark, Male) matches this filter both before and after, so nothing about
    // Petrov should change; Ivanov (John Male, Jane Female) does lose a member.
    await MainThread.InvokeOnMainThreadAsync(() => page.Filter.SexFilterIndex = 1);

    var ivanovAfter = page.Families.Single(f => f.Info.Value == "Ivanov");
    Assert.Equal(["John"], ivanovAfter.Persons.Select(p => p.DisplayName));

    var petrovAfter = page.Families.Single(f => f.Info.Value == "Petrov");
    Assert.Same(petrovBefore, petrovAfter);
    Assert.Same(petrovPersonsBefore, petrovAfter.Persons);
    Assert.Equal(0, petrovChangeCount);
    Assert.Equal(["Mark"], petrovAfter.Persons.Select(p => p.DisplayName));
  }

  [Fact]
  public async Task BindableLayout_reflects_incremental_persons_changes_without_recreating_unaffected_views()
  {
    // Exercises the same BindableLayout + FilteredObservableCollection<PersonInfo> mechanism
    // FamilyInfoItem.Persons uses in ProjectPage.xaml's per-card FlexLayout, without needing a
    // full ProjectPage/CollectionView setup.
    var showOnlyFemale = false;
    var family = new FamilyInfoItem(
      N(1, "Ivanov", NameType.FamilyName),
      [P(1, "John", BiologicalSex.Male), P(2, "Jane", BiologicalSex.Female)],
      (_, p) => !showOnlyFemale || p.BiologicalSex == BiologicalSex.Female);

    var flex = new FlexLayout();
    var template = new DataTemplate(() =>
    {
      var label = new Label();
      label.SetBinding(Label.TextProperty, nameof(PersonInfo.DisplayName));
      return label;
    });

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      BindableLayout.SetItemTemplate(flex, template);
      BindableLayout.SetItemsSource(flex, family.Persons);
    });

    var page = new ContentPage { Content = flex };
    await using var window = await WindowHost.AttachAsync(page);

    var childrenBefore = await MainThread.InvokeOnMainThreadAsync(() => flex.Children.ToList());
    Assert.Equal(2, childrenBefore.Count);
    var janeViewBefore = childrenBefore.Single(c => c is BindableObject { BindingContext: PersonInfo p } && p.DisplayName == "Jane");

    showOnlyFemale = true;
    await MainThread.InvokeOnMainThreadAsync(() => family.Update());

    var childrenAfter = await MainThread.InvokeOnMainThreadAsync(() => flex.Children.ToList());
    var janeViewAfter = Assert.Single(childrenAfter);
    Assert.Same(janeViewBefore, janeViewAfter);
  }
}
