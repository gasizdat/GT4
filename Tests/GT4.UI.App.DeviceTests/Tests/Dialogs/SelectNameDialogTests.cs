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

  private static async Task<SelectNameDialog> CreateDialogAsync(TestServices services, BiologicalSex biologicalSex = BiologicalSex.Male)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new SelectNameDialog(biologicalSex, services.Provider));
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var dialog = await CreateDialogAsync(new TestServices());

    Assert.Equal(3, dialog.NameTypes.Count);
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
}
