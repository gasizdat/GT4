using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Utils.Formatters;
using Moq;
using System.Text;
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

  // An imported OBJE whose TITL carries no text: the residue is there, the caption is not.
  private static Data BlankCaptionPhoto(int id)
  {
    var tagBytes = System.Text.Encoding.UTF8.GetBytes("0 OBJE\n1 TITL\n");
    byte[] content = [.. BitConverter.GetBytes(tagBytes.Length), .. tagBytes, 1, 2, 3];
    return new(id, content, MimeType: "image/png", Category: DataCategory.PersonMainPhotoTagged);
  }

  private static Data Attachment(int id, string fileName = "scan.pdf", string mimeType = "application/pdf") => new(
    id, Content: GedcomPhotoResidue.EncodeAttachment([1, 2, 3], fileName), MimeType: mimeType, Category: DataCategory.PersonAttachment);

  private static Data Bio(string markdown) =>
    new(ElementId.NonCommittedId, Encoding.UTF8.GetBytes(markdown), "text/plain", DataCategory.PersonBio);

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
    GedcomData: null,
    Attachments: []);

  private static async Task<CreateOrUpdatePersonDialog> CreateDialogAsync(TestServices services, PersonFullInfo? person)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<CreateOrUpdatePersonDialog.Factory>().Create(person));
  }

  private static AdornerCommandParameter Adorner(string commandName, object element) =>
    new() { CommandName = commandName, Element = element };

  private static PersonInfo P(int id, string firstName) =>
    new(id, default(Date), null, BiologicalSex.Male, [N(id, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  // The picker lists the project's Data table rather than the dialog's own edit buffers, so a media
  // test has to stock the project the sweep reads -- what the edited person holds is only what the
  // listing pins to the top of.
  private static void SetUpProjectMedia(
    TestServices services,
    Data[] dataSet,
    PersonInfo[]? persons = null,
    Dictionary<int, int[]>? personIdsByData = null)
  {
    services.Data
      .Setup(d => d.GetDataSetAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(dataSet);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync(persons ?? []);
    services.PersonData
      .Setup(p => p.GetPersonIdsByDataAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(personIdsByData ?? []);
  }

  private static Task<GalleryDataItem[]> WaitForMediaAsync(SelectMediaDialog dialog, int expectedCount) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog.Items.ToArray()),
      items => items.Length == expectedCount,
      timeoutMessage: $"SelectMediaDialog did not settle on {expectedCount} item(s); check the TestServices mock setup.");

  private static string CommonName(TestServices services, PersonInfo person)
  {
    var formatter = services.Provider.GetRequiredService<INameFormatter>();
    return formatter.ToString(person, NameFormat.CommonPersonName);
  }

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
    // PhotoTagDataConverter unwraps a tagged photo's residue envelope, so the fake content here must be
    // shaped like one (a real zero-length-tag prefix) rather than bare image bytes.
    var taggedPhoto = new Data(20, Content: [0, 0, 0, 0, 7, 7, 7], MimeType: "image/png", Category: DataCategory.PersonMainPhotoTagged);
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
  public async Task SaveCommand_UntouchedAttachment_SurvivesByteForByte()
  {
    // Guards the ObservableCollection<PersonDataItem>/ToDataAsync materialization added for
    // attachments: saving without touching them must not reconvert or drop the attachment.
    var services = new TestServices();
    var content = GedcomPhotoResidue.EncodeAttachment([4, 5, 6], "deed.pdf");
    var attachment = new Data(30, Content: content, MimeType: "application/pdf", Category: DataCategory.PersonAttachment);
    var person = CreateSamplePerson() with { Attachments = [attachment] };
    var dialog = await CreateDialogAsync(services, person);

    Assert.Single(dialog.Attachments);

    // Save is a no-op (treated as Cancel) unless something was modified; mark the dialog dirty via an
    // unrelated field (re-picking the same sex) without touching the attachment.
    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.BioSex = dialog.BiologicalSexes.Single(s => s.Info == BiologicalSex.Male));

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("CreatePersonCommand"));
    var saved = await dialog.Info;

    Assert.NotNull(saved);
    var savedAttachment = Assert.Single(saved.Attachments);
    Assert.Equal(DataCategory.PersonAttachment, savedAttachment.Category);
    Assert.Equal(attachment.Content, savedAttachment.Content);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task RemoveAttachmentCommand_removes_the_attachment_and_marks_modified()
  {
    var person = CreateSamplePerson() with { Attachments = [Attachment(30), Attachment(31)] };
    var dialog = await CreateDialogAsync(new TestServices(), person);
    var attachment = dialog.Attachments.ElementAt(0);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("RemoveAttachmentCommand", attachment)));

    await WaitForAsync(() => dialog.Attachments.Count, count => count == 1, "RemoveAttachmentCommand did not remove the attachment.");
    Assert.DoesNotContain(attachment, dialog.Attachments);
  }

  [Fact]
  public async Task MoveAttachmentDownCommand_reorders_attachments()
  {
    var person = CreateSamplePerson() with { Attachments = [Attachment(30, "first.pdf"), Attachment(31, "second.pdf")] };
    var dialog = await CreateDialogAsync(new TestServices(), person);
    var first = dialog.Attachments.ElementAt(0);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("MoveAttachmentDownCommand", first)));

    await WaitForAsync(() => dialog.Attachments.ToArray(), items => items.Length == 2 && items[1] == first,
      "MoveAttachmentDownCommand did not reorder the attachment.");
  }

  [Fact]
  public async Task MoveAttachmentUpCommand_on_the_first_item_reports_the_bounds_error()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { Attachments = [Attachment(30)] };
    var dialog = await CreateDialogAsync(services, person);
    var attachment = dialog.Attachments.Single();

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.DialogCommand.Execute(Adorner("MoveAttachmentUpCommand", attachment)));

    await WaitForAsync(
      () => services.AlertService.Invocations.Count, count => count > 0,
      "MoveAttachmentUpCommand did not report the bounds error.");
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<ApplicationException>()), Times.Once());
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
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(CreateSamplePerson()));

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
  public async Task InsertLinkCommand_inserts_a_person_link_via_SelectPersonDialog()
  {
    var services = new TestServices();
    var linkedPerson = new PersonInfo(
      2, Date.Create(1950, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male,
      [N(1, "Petrov", NameType.LastName), N(2, "Petr", NameType.FirstName | NameType.MaleDeclension)], null);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([linkedPerson]);
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(CreateSamplePerson()));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectPersonDialog>(dialog);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => selectDialog.Persons.Count()),
      count => count > 0,
      timeoutMessage: "SelectPersonDialog's person list never loaded.");

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.SelectedPerson = selectDialog.Persons.Single(p => p.Id == linkedPerson.Id);
      selectDialog.DialogCommand.Execute("SelectPersonCommand");
    });
    await insertTask;

    Assert.Equal("[Petr  Petrov](person:2)", dialog.Biography!.Content);
  }

  [Fact]
  public async Task InsertMediaLinkCommand_inserts_a_photo_link_via_SelectMediaDialog()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    SetUpProjectMedia(
      services,
      dataSet: [Photo(10), Photo(11), Photo(12)],
      persons: [ivan],
      personIdsByData: new() { [10] = [ivan.Id], [11] = [ivan.Id], [12] = [ivan.Id] });
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(CreateSamplePerson()));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, 3);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.SelectedItem = items.Single(i => i.Info.Id == 11);
      selectDialog.DialogCommand.Execute("SelectMediaCommand");
    });
    await insertTask;

    Assert.Equal($"![{CommonName(services, ivan)}](media:11)", dialog.Biography!.Content);
  }

  // A photo whose caption is blank has nothing of its own to be named by; project-wide, the person it
  // belongs to is the only label that still tells the user which photo they linked.
  [Fact]
  public async Task InsertMediaLinkCommand_names_a_blank_captioned_photo_by_its_owner()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var person = CreateSamplePerson() with { MainPhoto = BlankCaptionPhoto(13), AdditionalPhotos = [] };
    SetUpProjectMedia(
      services,
      dataSet: [BlankCaptionPhoto(13)],
      persons: [ivan],
      personIdsByData: new() { [13] = [ivan.Id] });
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, 1);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.SelectedItem = items.Single(i => i.Info.Id == 13);
      selectDialog.DialogCommand.Execute("SelectMediaCommand");
    });
    await insertTask;

    Assert.Equal($"![{CommonName(services, ivan)}](media:13)", dialog.Biography!.Content);
  }

  [Fact]
  public async Task InsertMediaLinkCommand_inserts_an_attachment_link_using_its_file_name()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { MainPhoto = null, AdditionalPhotos = [], Attachments = [Attachment(20, "scan.pdf")] };
    SetUpProjectMedia(services, dataSet: [Attachment(20, "scan.pdf")]);
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, 1);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.SelectedItem = items.Single(i => i.Info.Id == 20);
      selectDialog.DialogCommand.Execute("SelectMediaCommand");
    });
    await insertTask;

    Assert.Equal("[scan.pdf](attachment:20)", dialog.Biography!.Content);
  }

  [Fact]
  public async Task InsertMediaLinkCommand_inlines_an_image_attachment_as_a_media_link()
  {
    var services = new TestServices();
    var attachment = Attachment(21, "scan.jpg", "image/jpeg");
    var person = CreateSamplePerson() with { MainPhoto = null, AdditionalPhotos = [], Attachments = [attachment] };
    SetUpProjectMedia(services, dataSet: [attachment]);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(21, It.IsAny<CancellationToken>()))
      .ReturnsAsync(attachment);
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, 1);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.SelectedItem = items.Single(i => i.Info.Id == 21);
      selectDialog.DialogCommand.Execute("SelectMediaCommand");
    });
    await insertTask;

    Assert.Equal("![scan.jpg](media:21)", dialog.Biography!.Content);
    Assert.NotNull(await dialog.MediaResolver("media:21"));
  }

  // The picker and the resolver must agree on what can be embedded: offering an embed the resolver
  // rejects renders nothing, and withholding one it accepts wastes a working inline photo.
  [Fact]
  public async Task InsertMediaLinkCommand_offers_as_inline_exactly_what_the_resolver_can_render()
  {
    var services = new TestServices();
    var attachments = new[] { Attachment(20, "scan.pdf"), Attachment(21, "scan.jpg", "image/jpeg") };
    var person = CreateSamplePerson() with { Attachments = attachments };
    Data[] stored = [person.MainPhoto!, .. person.AdditionalPhotos, .. attachments];
    SetUpProjectMedia(services, dataSet: stored);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((int? id, CancellationToken _) => stored.FirstOrDefault(item => item.Id == id));
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, stored.Length);

    foreach (var item in items)
    {
      var media = await dialog.MediaResolver($"media:{item.Info.Id}");
      Assert.Equal(item.Info.IsInlineImage(), media is not null);
    }

    await MainThread.InvokeOnMainThreadAsync(() => selectDialog.DialogCommand.Execute("SelectMediaCommand"));
    await insertTask;
  }

  // Issue #300: the picker is project-wide, so another person's photo is a link target too. The
  // renderer already resolves any media id in the project, and the link is display-only either way.
  [Fact]
  public async Task InsertMediaLinkCommand_links_media_owned_by_another_person()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var petr = P(2, "Petr");
    SetUpProjectMedia(
      services,
      dataSet: [Photo(10), Photo(30)],
      persons: [ivan, petr],
      personIdsByData: new() { [10] = [ivan.Id], [30] = [petr.Id] });
    var person = CreateSamplePerson() with { MainPhoto = Photo(10), AdditionalPhotos = [] };
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, 2);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.SelectedItem = items.Single(i => i.Info.Id == 30);
      selectDialog.DialogCommand.Execute("SelectMediaCommand");
    });
    await insertTask;

    Assert.Equal($"![{CommonName(services, petr)}](media:30)", dialog.Biography!.Content);
  }

  // Project-wide candidates are only usable if the person being edited does not have to be hunted for
  // among them.
  [Fact]
  public async Task InsertMediaLinkCommand_lists_the_edited_persons_media_first()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var petr = P(2, "Petr");
    SetUpProjectMedia(
      services,
      dataSet: [Photo(30), Photo(31), Photo(12)],
      persons: [ivan, petr],
      personIdsByData: new() { [30] = [petr.Id], [31] = [petr.Id], [12] = [ivan.Id] });
    var person = CreateSamplePerson() with { MainPhoto = null, AdditionalPhotos = [Photo(12)] };
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    var items = await WaitForMediaAsync(selectDialog, 3);

    Assert.Equal([12, 30, 31], items.Select(item => item.Info.Id).ToArray());

    await MainThread.InvokeOnMainThreadAsync(() => selectDialog.DialogCommand.Execute("SelectMediaCommand"));
    await insertTask;
  }

  // The search filters on owners, not on the row's title: a title is resolved lazily per rendered row,
  // so filtering on it would decode every blob in the project on the first keystroke.
  [Fact]
  public async Task InsertMediaLinkCommand_filters_the_search_to_matching_owners()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var petr = P(2, "Petr");
    SetUpProjectMedia(
      services,
      dataSet: [Photo(10), Photo(30)],
      persons: [ivan, petr],
      personIdsByData: new() { [10] = [ivan.Id], [30] = [petr.Id] });
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dialog = await MainThread.InvokeOnMainThreadAsync(
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(CreateSamplePerson()));

    await using var window = await WindowHost.AttachAsync(dialog);
    var insertTask = await MainThreadTask.StartAsync(dialog.InvokeInsertMediaLinkAsync);
    var selectDialog = await ModalDialogHarness.WaitForModalAsync<SelectMediaDialog>(dialog);
    await WaitForMediaAsync(selectDialog, 2);

    var matched = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      selectDialog.OwnerFilter = "Petr";
      return selectDialog.Items.ToArray();
    });

    Assert.Equal([30], matched.Select(item => item.Info.Id).ToArray());

    await MainThread.InvokeOnMainThreadAsync(() => selectDialog.DialogCommand.Execute("SelectMediaCommand"));
    await insertTask;
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
      () => services.Provider.GetRequiredService<TestableCreateOrUpdatePersonDialog.Factory>().Create(person));
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
