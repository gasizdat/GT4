using GT4.Core.Project.Dto;
using GT4.UI.Dialogs;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SelectNameDialog's own uncovered logic directly. Its AddName/OnSelectName happy path is
/// already exercised through CreateOrUpdatePersonDialogTests.AddNameCommand_adds_the_selected_name_
/// via_SelectNameDialog (the only real caller); this file covers the Names cache/CurrentNameType
/// invalidation logic that test doesn't touch.
/// </summary>
public class SelectNameDialogTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static async Task<SelectNameDialog> CreateDialogAsync(
    TestServices services, BiologicalSex biologicalSex = BiologicalSex.Male, NameType? nameType = null)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    NameType[] nameTypes = nameType.HasValue ? [nameType.Value] : [NameType.FirstName, NameType.LastName];
    return await MainThread.InvokeOnMainThreadAsync(() => new SelectNameDialog(biologicalSex, nameTypes, services.Provider));
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var dialog = await CreateDialogAsync(new TestServices());

    Assert.Equal(2, dialog.NameTypes.Count);
    Assert.Equal(dialog.NameTypes.First(), dialog.CurrentNameType);
    Assert.Null(dialog.CurrentName);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Names_loads_with_the_composed_declension_for_the_current_type_and_sex()
  {
    var services = new TestServices();
    var firstNames = new[] { N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FirstName | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync(firstNames);
    var dialog = await CreateDialogAsync(services, BiologicalSex.Male);

    var names = dialog.Names;

    Assert.Single(names!);
    Assert.Equal("Ivan", names!.Single().Info.Value);
  }

  [Fact]
  public async Task Changing_CurrentNameType_invalidates_the_cached_names_and_clears_CurrentName()
  {
    var services = new TestServices();
    var firstNames = new[] { N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension) };
    var lastNames = new[] { N(2, "Ivanov", NameType.LastName | NameType.MaleDeclension) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FirstName | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync(firstNames);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.LastName | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync(lastNames);
    var dialog = await CreateDialogAsync(services, BiologicalSex.Male);
    Assert.Single(dialog.Names!); // force the FirstName load and cache it
    await MainThread.InvokeOnMainThreadAsync(() => dialog.CurrentName = dialog.Names!.Single());

    await MainThread.InvokeOnMainThreadAsync(
      () => dialog.CurrentNameType = dialog.NameTypes.Single(t => t.Type == NameType.LastName));

    Assert.Null(dialog.CurrentName);
    Assert.Single(dialog.Names!);
    Assert.Equal("Ivanov", dialog.Names!.Single().Info.Value);
  }

  [Fact]
  public async Task CurrentName_changes_the_dialog_button_name()
  {
    var services = new TestServices();
    var firstNames = new[] { N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FirstName | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync(firstNames);
    var dialog = await CreateDialogAsync(services, BiologicalSex.Male);
    var item = dialog.Names!.Single();

    await MainThread.InvokeOnMainThreadAsync(() => dialog.CurrentName = item);

    Assert.Equal(Resources.UIStrings.BtnNameOk, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Ctor_with_a_nameType_filter_shows_only_that_type()
  {
    var dialog = await CreateDialogAsync(new TestServices(), nameType: NameType.LastName);

    var onlyType = Assert.Single(dialog.NameTypes);
    Assert.Equal(NameType.LastName, onlyType.Type);
    Assert.Equal(NameType.LastName, dialog.CurrentNameType.Type);
  }

  [Fact]
  public async Task Names_for_the_FamilyName_type_queries_without_a_declension()
  {
    var services = new TestServices();
    var families = new[] { N(1, "Ivanov", NameType.FamilyName) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FamilyName, It.IsAny<CancellationToken>()))
      .ReturnsAsync(families);
    var dialog = await CreateDialogAsync(services, nameType: NameType.FamilyName);

    var names = dialog.Names;

    Assert.Single(names!);
    Assert.Equal("Ivanov", names!.Single().Info.Value);
    services.Names.Verify(
      n => n.GetNamesByTypeAsync(It.Is<NameType>(t => t != NameType.FamilyName), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  [Fact]
  public async Task OnAddNameAsync_when_current_type_is_LastName_creates_a_family()
  {
    var services = new TestServices();
    var newFamily = N(5, "Petrov", NameType.FamilyName);
    services.FamilyManager
      .Setup(f => f.AddFamilyAsync("Petrov", "Petrov", "Petrova", It.IsAny<CancellationToken>()))
      .ReturnsAsync(newFamily);
    var dialog = await CreateDialogAsync(services, nameType: NameType.LastName);
    await using var window = await WindowHost.AttachAsync(dialog);

    var addTask = await MainThreadTask.StartAsync(dialog.OnAddNameAsync);
    var nameDialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(dialog);
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      nameDialog.GeneralName = "Petrov";
      nameDialog.MaleName = "Petrov";
      nameDialog.FemaleName = "Petrova";
      nameDialog.DialogCommand.Execute(null);
    });
    await addTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync("Petrov", "Petrov", "Petrova", It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task OnAddNameAsync_when_current_type_is_FamilyName_creates_a_family_and_selects_it()
  {
    var services = new TestServices();
    var newFamily = N(5, "Petrov", NameType.FamilyName);
    services.FamilyManager
      .Setup(f => f.AddFamilyAsync("Petrov", "Petrov", "Petrova", It.IsAny<CancellationToken>()))
      .ReturnsAsync(newFamily);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FamilyName, It.IsAny<CancellationToken>()))
      .ReturnsAsync([newFamily]);
    var dialog = await CreateDialogAsync(services, nameType: NameType.FamilyName);
    await using var window = await WindowHost.AttachAsync(dialog);

    var addTask = await MainThreadTask.StartAsync(dialog.OnAddNameAsync);
    var nameDialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(dialog);
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      nameDialog.GeneralName = "Petrov";
      nameDialog.MaleName = "Petrov";
      nameDialog.FemaleName = "Petrova";
      nameDialog.DialogCommand.Execute(null);
    });
    await addTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync("Petrov", "Petrov", "Petrova", It.IsAny<CancellationToken>()), Times.Once());
    Assert.Equal(newFamily.Id, dialog.CurrentName?.Info.Id);
  }

  [Fact]
  public async Task OnAddNameAsync_when_current_type_carries_a_declension_bit_still_creates_a_name()
  {
    // Edit mode (CreateOrUpdatePersonDialog.OnEditPersonNameAsync) filters this dialog to the
    // edited name's own stored type, which always carries a declension bit (e.g.
    // FirstName|MaleDeclension) -- OnAddNameAsync must strip that bit before switching on it, or it
    // silently falls through to `default: return` and the Add button becomes a no-op.
    var services = new TestServices();
    var newFirstName = N(7, "Petr", NameType.FirstName | NameType.MaleDeclension);
    services.Names
      .Setup(n => n.AddFirstMaleNameAsync("Petr", "Petrovich", "Petrovna", It.IsAny<CancellationToken>()))
      .ReturnsAsync(newFirstName);
    var dialog = await CreateDialogAsync(services, nameType: NameType.FirstName | NameType.MaleDeclension);
    await using var window = await WindowHost.AttachAsync(dialog);

    var addTask = await MainThreadTask.StartAsync(dialog.OnAddNameAsync);
    var nameDialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(dialog);
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      nameDialog.GeneralName = "Petr";
      nameDialog.MaleName = "Petrovich";
      nameDialog.FemaleName = "Petrovna";
      nameDialog.DialogCommand.Execute(null);
    });
    await addTask;

    services.Names.Verify(
      n => n.AddFirstMaleNameAsync("Petr", "Petrovich", "Petrovna", It.IsAny<CancellationToken>()), Times.Once());
  }
}
