using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers NamesPage against a real MAUI runtime with a mocked Core. PageCommand("AddName") and
/// EditNameCommand push a modal CreateOrUpdateNameDialog via Navigation; those flows are driven
/// through WindowHost/ModalDialogHarness since a detached page can't resolve modal push/pop.
/// </summary>
public class NamesPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName) =>
    new(id, default(Date), null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  private static async Task<TestableNamesPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableNamesPage>());
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.Equal(4, page.NameTypes.Count);
    Assert.Equal(3, page.BiologicalSexes.Count);
    Assert.Equal(page.NameTypes.First(), page.CurrentNameType);
    Assert.Equal(page.BiologicalSexes.First(), page.CurrentBiologicalSex);
    Assert.NotNull(page.EditNameCommand);
    Assert.NotNull(page.DeleteNameCommand);
    Assert.NotNull(page.PageCommand);
  }

  [Fact]
  public async Task GetNamesAsync_sorts_with_the_registered_comparer()
  {
    var services = new TestServices();
    var unsorted = new[] { N(1, "Pushkin", NameType.FamilyName), N(2, "Aksakov", NameType.FamilyName), N(3, "Tolstoy", NameType.FamilyName) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(unsorted);
    var page = await CreatePageAsync(services);

    // The page's own CollectionView binding already consumed the ctor's automatic first load
    // before this test could subscribe to its completion; force a fresh one to observe.
    var names = await page.ReloadNamesAsync(() => page.InvokeRequestUpdateNames());

    Assert.Equal(["Aksakov", "Pushkin", "Tolstoy"], names.Select(n => n.Value));
  }

  public static IEnumerable<object[]> DeclensionCombinations()
  {
    yield return [NameType.FamilyName, BiologicalSex.Male, NameType.FamilyName];
    yield return [NameType.FamilyName, BiologicalSex.Female, NameType.FamilyName];
    yield return [NameType.FamilyName, BiologicalSex.Unknown, NameType.FamilyName];
    yield return [NameType.FirstName, BiologicalSex.Male, NameType.FirstName | NameType.MaleDeclension];
    yield return [NameType.FirstName, BiologicalSex.Female, NameType.FirstName | NameType.FemaleDeclension];
    yield return [NameType.FirstName, BiologicalSex.Unknown, NameType.FirstName];
    yield return [NameType.Patronymic, BiologicalSex.Female, NameType.Patronymic | NameType.FemaleDeclension];
    yield return [NameType.LastName, BiologicalSex.Male, NameType.LastName | NameType.MaleDeclension];
  }

  [Theory]
  [MemberData(nameof(DeclensionCombinations))]
  public async Task Selecting_a_name_type_and_sex_requests_the_composed_declension(
    NameType nameType, BiologicalSex sex, NameType expectedComposedType)
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    var targetType = page.NameTypes.Single(t => t.Type == nameType);
    var targetSex = page.BiologicalSexes.Single(s => s.Info == sex);

    // Setting CurrentNameType to its already-current value is a no-op (guarded by record
    // equality), so only CurrentBiologicalSex's unconditional setter is relied on to trigger the
    // reload that this assertion observes.
    await page.ReloadNamesAsync(() =>
    {
      page.CurrentNameType = targetType;
      page.CurrentBiologicalSex = targetSex;
    });

    services.Names.Verify(n => n.GetNamesByTypeAsync(expectedComposedType, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Setting_the_same_CurrentNameType_does_not_reload()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.ReloadNamesAsync(() => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;

    await MainThread.InvokeOnMainThreadAsync(() => page.CurrentNameType = page.CurrentNameType);

    Assert.Equal(callsBefore, services.Names.Invocations.Count);
  }

  [Fact]
  public async Task Setting_the_same_CurrentBiologicalSex_reloads_again()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.ReloadNamesAsync(() => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;

    await page.ReloadNamesAsync(() => page.CurrentBiologicalSex = page.CurrentBiologicalSex);

    Assert.True(services.Names.Invocations.Count > callsBefore);
  }

  [Fact]
  public async Task NameFilter_matches_case_insensitively_without_reloading()
  {
    var services = new TestServices();
    var loaded = new[] { N(1, "Anna", NameType.FirstName), N(2, "Ivan", NameType.FirstName), N(3, "Hanna", NameType.FirstName) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(loaded);
    var page = await CreatePageAsync(services);
    await page.ReloadNamesAsync(() => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;

    await MainThread.InvokeOnMainThreadAsync(() => page.NameFilter = "ANN");
    var filtered = await MainThread.InvokeOnMainThreadAsync(() => page.Names.ToArray());

    Assert.Equal(["Anna", "Hanna"], filtered.Select(n => n.Value));
    Assert.Equal(callsBefore, services.Names.Invocations.Count);

    await MainThread.InvokeOnMainThreadAsync(() => page.NameFilter = "");
    var restored = await MainThread.InvokeOnMainThreadAsync(() => page.Names.ToArray());

    Assert.Equal(3, restored.Length);
  }

  [Fact]
  public async Task CurrentName_resolves_to_the_requested_name_after_reload()
  {
    var services = new TestServices();
    var target = N(2, "Pushkin", NameType.FamilyName);
    var other = N(3, "Tolstoy", NameType.FamilyName);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([target, other]);
    var page = await CreatePageAsync(services);

    await page.ReloadNamesAsync(() => page.InvokeRequestUpdateNames(target));
    var currentName = await MainThread.InvokeOnMainThreadAsync(() => page.CurrentName);

    Assert.Equal(target.Id, currentName!.Id);

    page.InvokeRequestUpdateNames(null);
    var clearedName = await MainThread.InvokeOnMainThreadAsync(() => page.CurrentName);

    Assert.Null(clearedName);
  }

  [Fact]
  public async Task DeleteNameCommand_removes_the_name_and_requests_a_reload()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await page.ReloadNamesAsync(() => page.InvokeRequestUpdateNames());
    var loadsBefore = page.CompletedLoads;
    var name = N(5, "Pushkin", NameType.FamilyName);

    await page.InvokeDeleteAsync(name);
    // A load completing after the snapshot is the delete's own RequestUpdateNames landing.
    await Poll.UntilAsync(() => Task.FromResult(page.CompletedLoads), loads => loads > loadsBefore);

    services.Names.Verify(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()), Times.Once());
    services.PersonManager.Verify(p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task DeleteNameCommand_ignores_a_non_name_parameter()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await page.InvokeDeleteAsync(new object());

    services.Names.Verify(n => n.RemoveNameWithSubnamesAsync(It.IsAny<Name>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task DeleteNameCommand_reports_persons_sharing_the_root_name()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var name = N(5, "Pushkin", NameType.FamilyName);
    services.Names
      .Setup(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("FK constraint"));
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(name, false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, "Alexander")]);

    var ex = await Record.ExceptionAsync(() => page.InvokeDeleteAsync(name));

    var applicationException = Assert.IsType<ApplicationException>(ex);
    Assert.Contains("Pushkin", applicationException.Message);
    Assert.Contains("Alexander", applicationException.Message);
  }

  [Fact]
  public async Task DeleteNameCommand_reports_persons_sharing_a_subname()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var name = N(5, "Pushkin", NameType.FamilyName);
    var subname = N(6, "Pushkina", NameType.LastName | NameType.FemaleDeclension);
    services.Names
      .Setup(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("FK constraint"));
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(name, false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(name.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([subname]);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(subname, false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, "Natalia")]);

    var ex = await Record.ExceptionAsync(() => page.InvokeDeleteAsync(name));

    var applicationException = Assert.IsType<ApplicationException>(ex);
    Assert.Contains("Pushkina", applicationException.Message);
    Assert.Contains("Natalia", applicationException.Message);
  }

  [Fact]
  public async Task OnAddName_creates_a_family_name_and_selects_it()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services); // default CurrentNameType/CurrentBiologicalSex = FamilyName/Male
    var addedFamilyName = N(10, "Ivanov", NameType.FamilyName);
    services.FamilyManager
      .Setup(f => f.AddFamilyAsync("Ivanov", "Ivan", "Ivanova", It.IsAny<CancellationToken>()))
      .ReturnsAsync(addedFamilyName);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(addedFamilyName.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([addedFamilyName]);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FamilyName, It.IsAny<CancellationToken>()))
      .ReturnsAsync([addedFamilyName]);

    await using var window = await WindowHost.AttachAsync(page);
    var addTask = await MainThreadTask.StartAsync(page.InvokeAddNameAsync);
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Ivanov";
      dialog.MaleName = "Ivan";
      dialog.FemaleName = "Ivanova";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await addTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync("Ivanov", "Ivan", "Ivanova", It.IsAny<CancellationToken>()),
      Times.Once());

    var currentName = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.CurrentName),
      name => name?.Id == addedFamilyName.Id);
    Assert.Equal(addedFamilyName.Id, currentName!.Id);
  }

  [Fact]
  public async Task OnAddName_creates_a_male_first_name_and_selects_it()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var addedFirstName = N(11, "Ivan", NameType.FirstName | NameType.MaleDeclension);
    services.Names
      .Setup(n => n.AddFirstMaleNameAsync("Ivan", "Ivanovich", "Ivanovna", It.IsAny<CancellationToken>()))
      .ReturnsAsync(addedFirstName);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FirstName | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync([addedFirstName]);

    await using var window = await WindowHost.AttachAsync(page);
    await MainThread.InvokeOnMainThreadAsync(() => page.CurrentNameType = page.NameTypes.Single(t => t.Type == NameType.FirstName));
    var addTask = await MainThreadTask.StartAsync(page.InvokeAddNameAsync);
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Ivan";
      dialog.MaleName = "Ivanovich";
      dialog.FemaleName = "Ivanovna";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await addTask;

    services.Names.Verify(
      n => n.AddFirstMaleNameAsync("Ivan", "Ivanovich", "Ivanovna", It.IsAny<CancellationToken>()),
      Times.Once());

    var currentName = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.CurrentName),
      name => name?.Id == addedFirstName.Id);
    Assert.Equal(addedFirstName.Id, currentName!.Id);
  }

  [Fact]
  public async Task OnAddName_creates_a_female_first_name_and_selects_it()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var addedFirstName = N(12, "Anna", NameType.FirstName | NameType.FemaleDeclension);
    services.Names
      .Setup(n => n.AddFirstFemaleNameAsync("Anna", It.IsAny<CancellationToken>()))
      .ReturnsAsync(addedFirstName);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FirstName | NameType.FemaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync([addedFirstName]);

    await using var window = await WindowHost.AttachAsync(page);
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.CurrentNameType = page.NameTypes.Single(t => t.Type == NameType.FirstName);
      page.CurrentBiologicalSex = page.BiologicalSexes.Single(s => s.Info == BiologicalSex.Female);
    });
    var addTask = await MainThreadTask.StartAsync(page.InvokeAddNameAsync);
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Anna";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await addTask;

    services.Names.Verify(n => n.AddFirstFemaleNameAsync("Anna", It.IsAny<CancellationToken>()), Times.Once());

    var currentName = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.CurrentName),
      name => name?.Id == addedFirstName.Id);
    Assert.Equal(addedFirstName.Id, currentName!.Id);
  }

  [Fact]
  public async Task OnAddName_does_not_call_a_manager_when_the_dialog_is_cancelled()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var addTask = await MainThreadTask.StartAsync(page.InvokeAddNameAsync);
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    // No fields touched: dialog.NotReady stays true, so confirming completes the awaited Info with null.
    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty));
    await addTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task OnEditCommandAsync_updates_a_family_name_and_its_declensions()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var familyName = N(1, "Ivanov", NameType.FamilyName);
    var maleName = N(2, "Ivanov", NameType.LastName | NameType.MaleDeclension);
    var femaleName = N(3, "Ivanova", NameType.LastName | NameType.FemaleDeclension);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(familyName.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([familyName, maleName, femaleName]);

    await using var window = await WindowHost.AttachAsync(page);
    var editTask = await MainThreadTask.StartAsync(() => page.InvokeEditAsync(familyName));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Petrov";
      dialog.MaleName = "Petrov";
      dialog.FemaleName = "Petrova";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await editTask;

    var updatedFamilyName = familyName with { Value = "Petrov" };
    var updatedMaleName = maleName with { Value = "Petrov" };
    var updatedFemaleName = femaleName with { Value = "Petrova" };
    services.Names.Verify(n => n.UpdateName(updatedFamilyName, It.IsAny<CancellationToken>()), Times.Once());
    services.Names.Verify(n => n.UpdateName(updatedMaleName, It.IsAny<CancellationToken>()), Times.Once());
    services.Names.Verify(n => n.UpdateName(updatedFemaleName, It.IsAny<CancellationToken>()), Times.Once());
    services.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task OnEditCommandAsync_updates_an_orphan_declension_name_directly()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var orphanLastName = N(7, "Sidorov", NameType.LastName | NameType.MaleDeclension);

    await using var window = await WindowHost.AttachAsync(page);
    var editTask = await MainThreadTask.StartAsync(() => page.InvokeEditAsync(orphanLastName));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.GeneralName = "Sidorov-Updated";
      dialog.OnCreateFamilyBtn(dialog, EventArgs.Empty);
    });
    await editTask;

    var updatedOrphanLastName = orphanLastName with { Value = "Sidorov-Updated" };
    services.Names.Verify(
      n => n.UpdateName(updatedOrphanLastName, It.IsAny<CancellationToken>()),
      Times.Once());
    services.Names.Verify(
      n => n.TryGetNameWithSubnamesByIdAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task DeleteNameCommand_rethrows_the_original_exception_when_nothing_is_shared()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var name = N(5, "Pushkin", NameType.FamilyName);
    var originalException = new InvalidOperationException("FK constraint");
    services.Names
      .Setup(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()))
      .ThrowsAsync(originalException);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(name.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var ex = await Record.ExceptionAsync(() => page.InvokeDeleteAsync(name));

    Assert.Same(originalException, ex);
  }
}
