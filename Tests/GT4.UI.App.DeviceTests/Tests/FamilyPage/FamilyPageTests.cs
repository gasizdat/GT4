using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Pages;
using GT4.UI.Resources;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers FamilyPage against a real MAUI runtime with a mocked Core. PageCommand("CreatePerson")
/// and PageCommand("EditFamily") push modal dialogs via Navigation; those flows are driven through
/// WindowHost/ModalDialogHarness since a detached page can't resolve modal push/pop.
/// </summary>
public class FamilyPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName) =>
    new(id, default(Date), null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName)], null);

  private static async Task<TestableFamilyPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableFamilyPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.MemberItemTappedCommand);
    Assert.NotNull(page.PageCommand);
    Assert.Null(page.FamilyName);
    Assert.Empty(page.Persons);
  }

  [Fact]
  public async Task Persons_is_empty_until_FamilyName_is_set()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    Assert.Empty(page.Persons);

    services.PersonManager.Verify(
      p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task Persons_sorts_with_the_registered_comparer()
  {
    var services = new TestServices();
    var unsorted = new[] { P(1, "Viktor"), P(2, "Anna"), P(3, "Boris") };
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(familyName, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(unsorted);
    var page = await CreatePageAsync(services);

    var persons = await page.ReloadPersonsAsync(() => page.FamilyName = familyName);

    Assert.Equal(["Anna", "Boris", "Viktor"], persons.Select(p => p.DisplayName));
  }

  [Fact]
  public async Task MaritalStatusData_is_not_fetched_until_the_filter_panel_is_shown()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(familyName, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, "Anna")]);
    var page = await CreatePageAsync(services);

    await page.ReloadPersonsAsync(() => page.FamilyName = familyName);
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Never());

    await page.WaitForFilterDataAsync();
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task Navigating_to_another_family_while_the_filter_panel_is_open_refetches_filter_data()
  {
    var services = new TestServices();
    var ivanov = N(5, "Ivanov", NameType.FamilyName);
    var petrov = N(6, "Petrov", NameType.FamilyName);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(ivanov, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, "Anna")]);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(petrov, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(2, "Boris")]);
    var page = await CreatePageAsync(services);
    await page.ReloadPersonsAsync(() => page.FamilyName = ivanov);
    await page.WaitForFilterDataAsync();
    var loadsBefore = page.FilterDataLoads;

    // The panel stays open across the navigation, so IsFiltersVisible never flips -- the re-fetch
    // must come from ResetFilterData itself once the new family's persons have landed.
    await page.ReloadPersonsAsync(() => page.FamilyName = petrov);

    await Poll.UntilAsync(
      () => Task.FromResult(page.FilterDataLoads),
      loads => loads > loadsBefore,
      timeoutMessage: "Filter data was not re-fetched after navigating with the panel open.");

    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(
        It.Is<Person[]>(persons => persons.Length == 1 && persons[0].Id == 2),
        It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task Persons_reports_non_teardown_exceptions_via_AlertService()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    var failure = new InvalidOperationException("DB error");
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(familyName, true, It.IsAny<CancellationToken>()))
      .ThrowsAsync(failure);
    var page = await CreatePageAsync(services);

    // Setting FamilyName fires OnPropertyChanged(Persons), which the page's own live
    // BindableLayout.ItemsSource binding picks up and re-evaluates on its own schedule -- so the
    // getter can run once for that and again for this line's explicit read. Times.AtLeastOnce()
    // avoids depending on that race, mirroring NamesPageTests' reload-count assertions.
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);
    var persons = await MainThread.InvokeOnMainThreadAsync(() => page.Persons.ToArray());

    // The background fetch throws asynchronously, so the alert can still be in flight once the
    // lines above return -- wait for it instead of racing SafeTask.Run's completion.
    await Poll.UntilAsync(
      () => Task.FromResult(services.AlertService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Load failure was not reported.");

    Assert.Empty(persons);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Persons_swallows_the_project_teardown_race()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    // Persons blocks on .Result, which wraps the fault in AggregateException rather than rethrowing
    // it directly -- SafeTask.IsProjectTeardown recurses into AggregateException's inner exceptions
    // for exactly this reason.
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(familyName, true, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ObjectDisposedException(nameof(IProjectDocument)));
    var page = await CreatePageAsync(services);

    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);
    var persons = await MainThread.InvokeOnMainThreadAsync(() => page.Persons.ToArray());

    // No positive signal to poll for here -- a swallowed exception leaves nothing observable.
    // Prove the negative over a window instead of a single check right after triggering it.
    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(services.AlertService.Invocations.Count),
      count => count > 0,
      TimeSpan.FromMilliseconds(300),
      failureMessage: "The project-teardown race was not swallowed as expected.");

    Assert.Empty(persons);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task RemoveFamily_confirmed_removes_family_and_navigates_back()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    services.AlertService.Setup(a => a.ShowConfirmationAsync(It.IsAny<string>())).ReturnsAsync(true);
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);

    await page.InvokePageCommandAsync("RemoveFamily");

    services.FamilyManager.Verify(f => f.RemoveFamilyAsync(familyName, It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task RemoveFamily_shows_a_generic_confirmation()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    services.AlertService.Setup(a => a.ShowConfirmationAsync(It.IsAny<string>())).ReturnsAsync(true);
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);

    await page.InvokePageCommandAsync("RemoveFamily");

    services.AlertService.Verify(
      a => a.ShowConfirmationAsync(string.Format(UIStrings.AlertTextRemoveFamilyConfirmationText_1, "Ivanov")),
      Times.Once());
    services.PersonManager.Verify(
      p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), false, It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task RemoveFamily_cancelled_does_nothing()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    var page = await CreatePageAsync(services); // ShowConfirmationAsync defaults to false (unconfigured)
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);

    await page.InvokePageCommandAsync("RemoveFamily");

    services.FamilyManager.Verify(
      f => f.RemoveFamilyAsync(It.IsAny<Name>(), It.IsAny<CancellationToken>()), Times.Never());
    services.NavigationService.Verify(
      n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
  }

  [Fact]
  public async Task OpenPerson_navigates_to_PersonPage_with_the_tapped_person()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var tapped = P(1, "Anna");
    var expectedRoute = $"{typeof(PersonPage).Namespace}/{typeof(PersonPage).Name}";

    await page.InvokeOpenPersonAsync(tapped);

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => ReferenceEquals(d["PersonInfo"], tapped))),
      Times.Once());
  }

  [Fact]
  public async Task CreatePerson_modal_flow_adds_the_person_to_the_family()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    // SetUpPersonFamily's own input/output shape is exercised by GT4.Core.Project.Tests; here it's
    // a black box -- only that its result (not the dialog's raw output) is what gets persisted.
    var personWithFamily = PersonFullInfo.Empty with { Id = 42 };
    services.FamilyManager
      .Setup(f => f.SetUpPersonFamily(It.IsAny<PersonFullInfo>(), familyName))
      .Returns(personWithFamily);
    // AddPersonAsync returns the persisted person; an unconfigured call would default to null,
    // which FamilyPage now sorts into place through PersonFilter's predicate (the old plain
    // ObservableCollection.Insert never exercised a predicate, so this gap was previously silent).
    services.PersonManager
      .Setup(p => p.AddPersonAsync(personWithFamily, It.IsAny<CancellationToken>()))
      .ReturnsAsync(personWithFamily);
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("CreatePerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.BioSex = dialog.BiologicalSexes.Single(s => s.Info == BiologicalSex.Female);
      dialog.BirthDate = Date.Create(2000, 1, 1, DateStatus.WellKnown);
      dialog.DialogCommand.Execute("CreatePersonCommand");
    });
    await commandTask;

    services.PersonManager.Verify(p => p.AddPersonAsync(personWithFamily, It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task CreatePerson_cancelled_dialog_does_not_add_a_person()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = N(5, "Ivanov", NameType.FamilyName));

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("CreatePerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    // Neither BioSex nor BirthDate touched: dialog.NotReady stays true, so the command completes
    // the awaited Info with null.
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("CreatePersonCommand"));
    await commandTask;

    services.PersonManager.Verify(
      p => p.AddPersonAsync(It.IsAny<PersonFullInfo>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task EditFamily_updates_the_family_name()
  {
    var services = new TestServices();
    var familyName = N(5, "Ivanov", NameType.FamilyName);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(familyName.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([familyName]);
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.FamilyName = familyName);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditFamily"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    Assert.Equal("Ivanov", dialog.GeneralName);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Petrov";
      dialog.MaleName = "Petrov";
      dialog.FemaleName = "Petrova";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await commandTask;

    var updatedFamilyName = familyName with { Value = "Petrov" };
    services.Names.Verify(n => n.UpdateName(updatedFamilyName, It.IsAny<CancellationToken>()), Times.Once());
    services.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
  }
}
