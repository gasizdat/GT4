using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers CreateOrUpdateNameDialog's family photo/attachment staging (issue #190): the
/// ShowFamilyMedia gate, and that every media mutation marks the dialog modified -- a media-only
/// edit (no name/text touched) must still enable save, not silently stay "Cancel" and drop the
/// edit. Photo/attachment *picking* is out of scope here (FilePicker is an external dependency,
/// same reasoning as CreateOrUpdatePersonDialogTests); reordering/removing already-present items
/// needs no picker at all.
/// </summary>
public class CreateOrUpdateNameDialogTests
{
  private static Name Family(int id = 1, string value = "Ivanov") => new(id, value, NameType.FamilyName, null);

  private static Name MaleLastName(int familyId = 1) => new(familyId + 100, "Ivanov", NameType.LastName | NameType.MaleDeclension, familyId);

  private static Name FemaleLastName(int familyId = 1) => new(familyId + 200, "Ivanova", NameType.LastName | NameType.FemaleDeclension, familyId);

  private static Data Photo(int id, DataCategory category = DataCategory.FamilyPhoto) =>
    new(id, Content: [1, 2, 3], MimeType: "image/png", Category: category);

  private static Data Attachment(int id, string fileName = "deed.pdf") =>
    new(id, Content: GedcomPhotoResidue.EncodeAttachment([1, 2, 3], fileName), MimeType: "application/pdf", Category: DataCategory.FamilyAttachment);

  private static AdornerCommandParameter Adorner(string commandName, object element) =>
    new() { CommandName = commandName, Element = element };

  private static async Task<CreateOrUpdateNameDialog> CreateDialogAsync(TestServices services, Data[]? familyData = null)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new CreateOrUpdateNameDialog(
      Family(),
      MaleLastName(),
      FemaleLastName(),
      services.Provider.GetRequiredService<INameTypeFormatter>(),
      services.AlertService.Object,
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.Provider.GetRequiredService<DataConverterResolver>(),
      familyData));
  }

  private static async Task<CreateOrUpdateNameDialog> CreateDialogForTypeAsync(TestServices services, NameType nameType)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new CreateOrUpdateNameDialog(
      nameType,
      services.Provider.GetRequiredService<INameTypeFormatter>(),
      services.AlertService.Object,
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.Provider.GetRequiredService<DataConverterResolver>()));
  }

  private static Task<T> WaitForAsync<T>(Func<T> probe, Func<T, bool> isReady, string message) =>
    Poll.UntilAsync(() => MainThread.InvokeOnMainThreadAsync(probe), isReady, timeoutMessage: message);

  [Fact]
  public async Task ShowFamilyMedia_is_true_only_for_the_FamilyName_type()
  {
    var familyDialog = await CreateDialogForTypeAsync(new TestServices(), NameType.FamilyName);
    var firstNameDialog = await CreateDialogForTypeAsync(new TestServices(), NameType.FirstName | NameType.MaleDeclension);

    Assert.True(familyDialog.ShowFamilyMedia);
    Assert.False(firstNameDialog.ShowFamilyMedia);
  }

  [Fact]
  public async Task Ctor_with_existing_family_data_populates_photos_and_attachments_unmodified()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto), Attachment(2)]);

    Assert.Single(dialog.Photos);
    Assert.Single(dialog.Attachments);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task RemoveFamilyPhotoCommand_removes_the_photo_and_marks_modified()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto), Photo(2)]);
    var photo = dialog.Photos.ElementAt(1);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.MediaCommand.Execute(Adorner("RemoveFamilyPhotoCommand", photo)));

    await WaitForAsync(() => dialog.Photos.Count, count => count == 1, "RemoveFamilyPhotoCommand did not remove the photo.");
    Assert.NotEqual(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task RemoveFamilyAttachmentCommand_marks_modified_without_any_text_change()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Attachment(1)]);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.MediaCommand.Execute(Adorner("RemoveFamilyAttachmentCommand", dialog.Attachments.Single())));

    await WaitForAsync(() => dialog.Attachments.Count, count => count == 0, "RemoveFamilyAttachmentCommand did not remove the attachment.");
    Assert.NotEqual(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task MoveFamilyPhotoToRightCommand_reorders_the_photos()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto), Photo(2)]);
    var first = dialog.Photos.ElementAt(0);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.MediaCommand.Execute(Adorner("MoveFamilyPhotoToRightCommand", first)));

    await WaitForAsync(
      () => dialog.Photos.ElementAt(1), item => ReferenceEquals(item, first),
      "MoveFamilyPhotoToRightCommand did not reorder the photos.");
  }

  [Fact]
  public async Task SaveCommand_reassigns_categories_by_final_position_not_add_time_category()
  {
    // Position 0 is authoritative for main-vs-additional at save time, regardless of the category
    // each item carried in: reordering must flip a previously-additional photo to FamilyMainPhoto.
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto), Photo(2)]);
    var wasAdditional = dialog.Photos.ElementAt(1);

    await MainThread.InvokeOnMainThreadAsync(() =>
      dialog.MediaCommand.Execute(Adorner("MoveFamilyPhotoToLeftCommand", wasAdditional)));
    await WaitForAsync(
      () => dialog.Photos.ElementAt(0), item => ReferenceEquals(item, wasAdditional),
      "MoveFamilyPhotoToLeftCommand did not reorder the photos.");

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var info = await dialog.Info;

    Assert.NotNull(info);
    Assert.Equal(2, info.Photos.Length);
    Assert.Equal(DataCategory.FamilyMainPhoto, info.Photos.Single(p => p.Id == 2).Category);
    Assert.Equal(DataCategory.FamilyPhoto, info.Photos.Single(p => p.Id == 1).Category);
  }
}
