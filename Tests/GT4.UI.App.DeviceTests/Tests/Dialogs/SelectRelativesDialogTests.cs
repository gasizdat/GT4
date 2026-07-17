using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SelectRelativesDialog's own command switch directly (constructed with the real
/// TestServices container, same shape as CreateOrUpdatePersonDialog). The Persons list's background
/// load/filter (Task.Run, triggered lazily by the Persons getter itself with no completion signal)
/// is not covered: repeatedly polling the getter to detect completion would repeatedly re-trigger
/// the load, risking duplicate items -- a real flakiness trap, not just extra effort.
/// </summary>
public class SelectRelativesDialogTests
{
  private static async Task<TestableSelectRelativesDialog> CreateDialogAsync(
    TestServices services, BiologicalSex? biologicalSex = null, Relative[]? existingRelatives = null)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(
      () => new TestableSelectRelativesDialog(biologicalSex, existingRelatives ?? [], services.Provider));
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var dialog = await CreateDialogAsync(new TestServices());

    Assert.Equal(3, dialog.BiologicalSexes.Length);
    Assert.Equal(5, dialog.RelationshipTypes.Length);
    Assert.Equal(BiologicalSex.Unknown, dialog.BioSex.Info);
    Assert.Equal(dialog.RelationshipTypes[0], dialog.RelType);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Ctor_with_a_specific_biological_sex_selects_it()
  {
    var dialog = await CreateDialogAsync(new TestServices(), BiologicalSex.Female);

    Assert.Equal(BiologicalSex.Female, dialog.BioSex.Info);
  }

  [Fact]
  public async Task DialogButtonName_reflects_whether_a_person_is_selected()
  {
    var dialog = await CreateDialogAsync(new TestServices());
    var person = new PersonInfo(1, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male, [], null);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.SelectedItems.Add(person));
    Assert.Equal(Resources.UIStrings.BtnNameOk, dialog.DialogButtonName);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.SelectedItems.Remove(person));
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task EditRelationshipDateCommand_modal_sets_the_relationship_date()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services);

    await using var window = await WindowHost.AttachAsync(dialog);
    var commandTask = await MainThreadTask.StartAsync(() => dialog.InvokeDialogCommandAsync("EditRelationshipDateCommand"));
    var dateDialog = await ModalDialogHarness.WaitForModalAsync<SelectDateDialog>(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dateDialog.Year = "1990";
      dateDialog.DialogCommand.Execute(null);
    });
    await commandTask;

    // Only Year was touched: YearSwitch turns on but MonthSwitch/DaySwitch stay off, so the
    // resulting status is MonthUnknown (see SelectDateDialogTests for the full cascade rules).
    Assert.Equal(Date.Create(1990, 0, 0, DateStatus.MonthUnknown), dialog.RelationshipDate);
  }

  [Fact]
  public async Task RemoveRelationshipDateCommand_clears_the_date()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.RelationshipDate = Date.Create(2000, 1, 1, DateStatus.WellKnown));

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("RemoveRelationshipDateCommand"));

    Assert.Null(dialog.RelationshipDate);
  }

  [Fact]
  public async Task SelectPersonCommand_builds_a_relative_info_per_selected_person()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services);
    var person = new PersonInfo(1, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male, [], null);
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.SelectedItems.Add(person);
      dialog.RelType = dialog.RelationshipTypes.Single(t => t.Info == RelationshipType.Spouse);
      dialog.RelationshipDate = Date.Create(2015, 6, 1, DateStatus.WellKnown);
      dialog.DialogCommand.Execute("SelectPersonCommand");
    });

    var result = await dialog.Info;

    var relative = Assert.Single(result!);
    Assert.Equal(person.Id, relative.Id);
    Assert.Equal(RelationshipType.Spouse, relative.Type);
    Assert.Equal(Date.Create(2015, 6, 1, DateStatus.WellKnown), relative.Date);
  }
}
