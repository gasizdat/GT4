using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers CreateOrUpdatePersonDialog's own command switch directly (constructed with the real
/// TestServices container, mirroring how FamilyPage/PersonPage build it): the branches no page flow
/// reaches (name/photo reordering and its bounds check, photo/name/relative removal, undefined
/// dates) plus one push-modal branch (AddNameCommand) to prove that wiring too. Photo *picking* is
/// out of scope -- FilePicker is an external dependency -- but reordering/removing already-present
/// photos needs no picker at all.
/// </summary>
public class CreateOrUpdatePersonDialogTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static Data Photo(int id) => new(id, Content: [1, 2, 3], MimeType: "image/png", Category: DataCategory.PersonPhoto);

  private static PersonFullInfo CreateSamplePerson() => new(
    Id: 1,
    BirthDate: Date.Create(2000, 1, 1, DateStatus.WellKnown),
    DeathDate: null,
    BiologicalSex: BiologicalSex.Male,
    Names: [N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension)],
    MainPhoto: Photo(10),
    AdditionalPhotos: [Photo(11), Photo(12)],
    RelativeInfos: [],
    Biography: null,
    GedcomData: null);

  private static async Task<CreateOrUpdatePersonDialog> CreateDialogAsync(TestServices services, PersonFullInfo? person)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<Func<PersonFullInfo?, CreateOrUpdatePersonDialog>>()(person));
  }

  private static AdornerCommandParameter Adorner(string commandName, object element) =>
    new() { CommandName = commandName, Element = element };

  /// <summary>
  /// DialogCommand.Execute is fire-and-forget (ICommand.Execute returns void), so even though
  /// OnDialogCommand's branches here have no real async gaps and normally complete inline, nothing
  /// guarantees that -- polling the actual post-condition avoids depending on that timing.
  /// </summary>
  private static Task<T> WaitForAsync<T>(Func<T> probe, Func<T, bool> isReady, string message) =>
    Poll.UntilAsync(() => MainThread.InvokeOnMainThreadAsync(probe), isReady, timeoutMessage: message);

  [Fact]
  public async Task Ctor_with_an_existing_person_populates_fields_unmodified()
  {
    var dialog = await CreateDialogAsync(new TestServices(), CreateSamplePerson());

    Assert.Equal(3, dialog.Photos.Count);
    Assert.Single(dialog.Names);
    Assert.Equal(BiologicalSex.Male, dialog.BioSex!.Info);
    Assert.Equal(Date.Create(2000, 1, 1, DateStatus.WellKnown), dialog.BirthDate);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Ctor_with_no_person_starts_empty()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    Assert.Empty(dialog.Photos);
    Assert.Empty(dialog.Names);
    Assert.Empty(dialog.Relatives);
    Assert.Null(dialog.BioSex);
    Assert.Null(dialog.BirthDate);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task RemoveNameCommand_removes_the_name_and_marks_modified()
  {
    var dialog = await CreateDialogAsync(new TestServices(), CreateSamplePerson());
    var name = dialog.Names.Single();

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("RemoveNameCommand", name)));

    await WaitForAsync(
      () => dialog.DialogButtonName, buttonName => buttonName != Resources.UIStrings.BtnNameCancel,
      "RemoveNameCommand did not mark the dialog modified.");
    Assert.Empty(dialog.Names);
  }

  [Fact]
  public async Task RemoveNameCommand_does_not_remove_a_FamilyName_even_when_invoked_directly()
  {
    // The delete adorner's IsVisible={Binding CanBeRemoved} hides the button for a FamilyName, but
    // that's a view-layer guard only; the command handler itself must also refuse, since any other
    // caller of DialogCommand (as this test does) bypasses the hidden button entirely.
    var familyName = N(2, "Ivanov", NameType.FamilyName);
    var person = CreateSamplePerson() with
    {
      Names = [N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension), familyName]
    };
    var dialog = await CreateDialogAsync(new TestServices(), person);
    var name = dialog.Names.Single(n => n.Info.Type == NameType.FamilyName);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("RemoveNameCommand", name)));

    Assert.Contains(dialog.Names, item => item.Info.Id == familyName.Id);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task MoveNameDownCommand_reorders_names()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with
    {
      Names = [N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension), N(2, "Ivanovich", NameType.Patronymic | NameType.MaleDeclension)]
    };
    var dialog = await CreateDialogAsync(services, person);
    var first = dialog.Names.ElementAt(0);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("MoveNameDownCommand", first)));

    await WaitForAsync(
      () => dialog.Names.ElementAt(1), item => ReferenceEquals(item, first),
      "MoveNameDownCommand did not reorder the names.");
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task MoveNameUpCommand_on_the_first_item_reports_the_bounds_error()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services, CreateSamplePerson());
    var first = dialog.Names.Single();

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("MoveNameUpCommand", first)));

    // MoveItem's bounds check throws ApplicationException; DialogCommand is a SafeCommand, so it's
    // routed to IAlertService rather than propagated to the caller.
    await WaitForAsync(
      () => services.AlertService.Invocations.Count, count => count > 0,
      "MoveNameUpCommand did not report the bounds error.");
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<ApplicationException>()), Times.Once());
  }

  [Fact]
  public async Task RemovePhotoCommand_removes_the_photo_and_marks_modified()
  {
    var dialog = await CreateDialogAsync(new TestServices(), CreateSamplePerson());
    var photo = dialog.Photos.ElementAt(1);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("RemovePhotoCommand", photo)));

    await WaitForAsync(() => dialog.Photos.Count, count => count == 2, "RemovePhotoCommand did not remove the photo.");
    Assert.DoesNotContain(photo, dialog.Photos);
  }

  [Fact]
  public async Task SaveCommand_UntouchedTaggedMainPhoto_SurvivesByteForByte()
  {
    // Guards the _IsModified short-circuit in PersonDataItem.ToDataAsync + the .AsMainPhoto() fix in
    // OnCreatePersonCommandAsync: saving without touching photos must not reconvert (which would
    // silently regenerate Content as plain bytes) or collapse the category to plain.
    var services = new TestServices();
    var taggedPhoto = new Data(20, Content: [7, 7, 7], MimeType: "image/png", Category: DataCategory.PersonMainPhotoTagged);
    var person = CreateSamplePerson() with { MainPhoto = taggedPhoto, AdditionalPhotos = [] };
    var dialog = await CreateDialogAsync(services, person);

    // Save is a no-op (treated as Cancel) unless something was modified; mark the dialog dirty via an
    // unrelated field (re-picking the same sex) without touching any photo.
    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.BioSex = dialog.BiologicalSexes.Single(s => s.Info == BiologicalSex.Male));

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("CreatePersonCommand"));
    var saved = await dialog.Info;

    Assert.NotNull(saved);
    Assert.NotNull(saved.MainPhoto);
    Assert.Equal(DataCategory.PersonMainPhotoTagged, saved.MainPhoto.Category);
    Assert.Equal(taggedPhoto.Content, saved.MainPhoto.Content);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task MovePhotoToRightCommand_on_the_last_item_reports_the_bounds_error()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services, CreateSamplePerson());
    var last = dialog.Photos.Last();

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("MovePhotoToRightCommand", last)));

    await WaitForAsync(
      () => services.AlertService.Invocations.Count, count => count > 0,
      "MovePhotoToRightCommand did not report the bounds error.");
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<ApplicationException>()), Times.Once());
  }

  [Fact]
  public async Task UndefinedBirthDateCommand_sets_the_unknown_sentinel()
  {
    var dialog = await CreateDialogAsync(new TestServices(), CreateSamplePerson());

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("UndefinedBirthDateCommand"));

    await WaitForAsync(
      () => dialog.BirthDate, date => date == Date.Create(0, DateStatus.Unknown),
      "UndefinedBirthDateCommand did not set the unknown sentinel.");
  }

  [Fact]
  public async Task RemoveDeathDateCommand_clears_the_death_date()
  {
    var person = CreateSamplePerson() with { DeathDate = Date.Create(2020, 1, 1, DateStatus.WellKnown) };
    var dialog = await CreateDialogAsync(new TestServices(), person);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("RemoveDeathDateCommand"));

    await WaitForAsync(() => dialog.DeathDate, date => date is null, "RemoveDeathDateCommand did not clear the death date.");
  }

  [Fact]
  public async Task AddNameCommand_warns_when_biological_sex_is_unknown()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { BiologicalSex = BiologicalSex.Unknown };
    var dialog = await CreateDialogAsync(services, person);
    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.BioSex = dialog.BiologicalSexes.Single(s => s.Info == BiologicalSex.Unknown));

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("AddNameCommand"));

    await WaitForAsync(
      () => services.AlertService.Invocations.Count, count => count > 0,
      "AddNameCommand did not warn about the unknown biological sex.");
    services.AlertService.Verify(a => a.ShowWarningAsync(It.IsAny<string>()), Times.Once());
  }

  [Fact]
  public async Task AddNameCommand_adds_the_selected_name_via_SelectNameDialog()
  {
    var services = new TestServices();
    var newPatronymic = N(9, "Ivanovich", NameType.Patronymic | NameType.MaleDeclension);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.Patronymic | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync([newPatronymic]);
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<Func<PersonFullInfo?, TestableCreateOrUpdatePersonDialog>>()(CreateSamplePerson()));

    await using var window = await WindowHost.AttachAsync(dialog);
    var addTask = await MainThreadTask.StartAsync(dialog.InvokeAddPersonNameAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectNameDialog>(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.CurrentNameType = selectDialog.NameTypes.Single(t => t.Type == NameType.Patronymic);
      selectDialog.CurrentName = selectDialog.Names!.Single(n => n.Info.Id == newPatronymic.Id);
      selectDialog.OnSelectName();
    });
    await addTask;

    Assert.Contains(dialog.Names, item => item.Info.Id == newPatronymic.Id);
  }

  [Fact]
  public async Task EditNameCommand_filters_SelectNameDialog_to_the_edited_names_own_type()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with
    {
      Names = [N(1, "Ivan", NameType.FirstName | NameType.MaleDeclension), N(2, "Ivanovich", NameType.Patronymic | NameType.MaleDeclension)]
    };
    var updatedPatronymic = N(9, "Petrovich", NameType.Patronymic | NameType.MaleDeclension);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.Patronymic | NameType.MaleDeclension, It.IsAny<CancellationToken>()))
      .ReturnsAsync([updatedPatronymic]);
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<Func<PersonFullInfo?, TestableCreateOrUpdatePersonDialog>>()(person));
    var patronymic = dialog.Names.Single(n => n.Info.Type == (NameType.Patronymic | NameType.MaleDeclension));

    await using var window = await WindowHost.AttachAsync(dialog);
    var editTask = await MainThreadTask.StartAsync(() => dialog.InvokeEditPersonNameAsync(patronymic));
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectNameDialog>(dialog);

    // The dialog is filtered to the edited name's own type: no other type to switch to.
    var onlyType = Assert.Single(selectDialog.NameTypes);
    Assert.Equal(NameType.Patronymic | NameType.MaleDeclension, onlyType.Type);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.CurrentName = selectDialog.Names!.Single(n => n.Info.Id == updatedPatronymic.Id);
      selectDialog.OnSelectName();
    });
    await editTask;

    Assert.Contains(dialog.Names, item => item.Info.Id == updatedPatronymic.Id);
  }
}
