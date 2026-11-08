using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using Moq;
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

  private static Data ImageAttachment(int id) =>
    new(id, Content: GedcomPhotoResidue.EncodeAttachment([1, 2, 3], "deed.png"), MimeType: "image/png", Category: DataCategory.FamilyAttachment);

  private static AdornerCommandParameter Adorner(string commandName, object element) =>
    new() { CommandName = commandName, Element = element };

  private static async Task<CreateOrUpdateNameDialog> CreateDialogAsync(
    TestServices services, Data[]? familyData = null, PhotoEnhancer? photoEnhancer = null)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var family = familyData is null
      ? null
      : new FamilyFullInfo(
          Family(),
          familyData.FirstOrDefault(data => data.Category.IsMainPhoto()),
          [.. familyData.Where(data => data.Category.IsAdditionalPhoto())],
          [.. familyData.Where(data => data.Category.IsAttachment())]);
    var enhancer = photoEnhancer ?? new TestablePhotoEnhancer(services, []);
    return await MainThread.InvokeOnMainThreadAsync(() => new CreateOrUpdateNameDialog(
      Family(),
      MaleLastName(),
      FemaleLastName(),
      services.Provider.GetRequiredService<INameTypeFormatter>(),
      services.AlertService.Object,
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.Provider.GetRequiredService<DataConverterResolver>(),
      enhancer,
      family));
  }

  private static async Task<CreateOrUpdateNameDialog> CreateDialogForTypeAsync(
    TestServices services, NameType nameType, PhotoEnhancer? photoEnhancer = null)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var enhancer = photoEnhancer ?? services.Provider.GetRequiredService<PhotoEnhancer>();
    return await MainThread.InvokeOnMainThreadAsync(() => new CreateOrUpdateNameDialog(
      nameType,
      services.Provider.GetRequiredService<INameTypeFormatter>(),
      services.AlertService.Object,
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.Provider.GetRequiredService<DataConverterResolver>(),
      enhancer));
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
  public async Task Loading_an_existing_attachments_caption_in_the_background_does_not_mark_the_dialog_modified()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Attachment(1)]);
    var attachment = dialog.Attachments.Single();

    await WaitForAsync(() => attachment.Content, content => content is not null, "Attachment content never finished loading.");

    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Ctor_seeds_the_main_photo_first_regardless_of_the_provided_order()
  {
    // The provided array puts the additional photo before the main photo (arbitrary DB row order);
    // position 0 is authoritative for main-vs-additional at save time (see
    // SaveCommand_reassigns_categories_by_final_position_not_add_time_category below), so the ctor
    // must not just walk the array as given.
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(2, DataCategory.FamilyPhoto), Photo(1, DataCategory.FamilyMainPhoto)]);

    Assert.Equal(1, dialog.Photos.ElementAt(0).Info.Id);
    Assert.Equal(2, dialog.Photos.ElementAt(1).Info.Id);
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

  [Fact]
  public async Task SaveCommand_encodes_a_modified_photo_through_its_converter()
  {
    // Saving item.Info instead of ToDataAsync() would silently discard whatever the item's Content
    // was edited to -- the converter is the only thing that turns an edited object back into Data.
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto)]);
    var photo = dialog.Photos.Single();

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      photo.Content = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([9, 9, 9])), null);
      dialog.GeneralName = "Petrov";
    });
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var info = await dialog.Info;

    Assert.NotNull(info);
    var saved = Assert.Single(info.Photos);
    Assert.Equal<byte[]>([9, 9, 9], saved.Content);
    // Reconverted content has no committed row behind it yet.
    Assert.Equal(ElementId.NonCommittedId, saved.Id);
  }

  // The copy's caption mark makes the tagged-photo converter re-envelope it, and that converter stamps
  // PersonPhotoTagged on whatever it re-envelopes.
  [Theory]
  [InlineData(null, DataCategory.FamilyMainPhoto)]
  [InlineData("Wedding", DataCategory.FamilyMainPhotoTagged)]
  public async Task EnhanceFamilyPhotoCommand_saves_the_copy_next_to_its_source_with_a_family_category(
    string? caption, DataCategory sourceCategory)
  {
    var services = new TestServices();
    var content = caption is null ? TestImages.ValidPng : GedcomPhotoResidue.EncodePhotoTitle(TestImages.ValidPng, caption);
    var enhancement = new DoublingEnhancement();
    var enhancer = new TestablePhotoEnhancer(services, [enhancement]) { EnhancementAnswer = enhancement.DisplayName };
    var dialog = await CreateDialogAsync(services, [new Data(10, content, "image/png", sourceCategory)], enhancer);
    await using var window = await WindowHost.AttachAsync(dialog);
    var source = dialog.Photos.Single();

    await EnhanceAdorners.TapAsync(dialog, dialog.Photos, "EnhanceFamilyPhotoCommand", source);
    await WaitForAsync(() => dialog.Photos.Count, count => count == 2, "The enhanced photo was never added.");
    Assert.Same(source, dialog.Photos.ElementAt(0));
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var info = await dialog.Info;

    Assert.NotNull(info);
    var ids = info.Photos.Select(photo => photo.Id);
    var categories = info.Photos.Select(photo => photo.Category);
    Assert.Equal(new[] { 10, ElementId.NonCommittedId }, ids);
    Assert.Equal(new[] { sourceCategory, DataCategory.FamilyPhotoTagged }, categories);
    Assert.NotNull(enhancement.Received);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task Only_an_image_attachment_offers_the_enhance_adorner()
  {
    var services = new TestServices();
    var enhancer = new TestablePhotoEnhancer(services, [new DoublingEnhancement()]);
    var dialog = await CreateDialogAsync(services, [ImageAttachment(1), Attachment(2)], enhancer);
    await using var window = await WindowHost.AttachAsync(dialog);

    var shown = await EnhanceAdorners.AttachmentsShownAsync(dialog, dialog.Attachments, "EnhanceFamilyAttachmentCommand");

    Assert.True(shown.Image);
    Assert.False(shown.Document);
  }

  [Fact]
  public async Task No_attachment_enhance_adorner_shows_when_no_enhancement_is_registered()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [ImageAttachment(1), Attachment(2)]);
    await using var window = await WindowHost.AttachAsync(dialog);

    var shown = await EnhanceAdorners.AttachmentsShownAsync(dialog, dialog.Attachments, "EnhanceFamilyAttachmentCommand");

    Assert.False(shown.Image);
    Assert.False(shown.Document);
  }

  [Fact]
  public async Task EnhanceFamilyAttachmentCommand_saves_the_copy_after_its_source_as_a_family_attachment()
  {
    var services = new TestServices();
    var scan = new Data(10, GedcomPhotoResidue.EncodeAttachment(TestImages.ValidPng, "deed.png"), "image/png", DataCategory.FamilyAttachment);
    var enhancement = new DoublingEnhancement();
    var enhancer = new TestablePhotoEnhancer(services, [enhancement]) { EnhancementAnswer = enhancement.DisplayName };
    var dialog = await CreateDialogAsync(services, [Photo(1, DataCategory.FamilyMainPhoto), scan, Attachment(11)], enhancer);
    await using var window = await WindowHost.AttachAsync(dialog);
    var source = dialog.Attachments.First();

    await EnhanceAdorners.TapAsync(dialog, dialog.Attachments, "EnhanceFamilyAttachmentCommand", source);
    await WaitForAsync(() => dialog.Attachments.Count, count => count == 3, "The enhanced attachment was never added.");
    Assert.Single(dialog.Photos);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var info = await dialog.Info;

    Assert.NotNull(info);
    var photo = Assert.Single(info.Photos);
    Assert.Equal(1, photo.Id);
    var ids = info.Attachments.Select(attachment => attachment.Id);
    var categories = info.Attachments.Select(attachment => attachment.Category).Distinct();
    Assert.Equal(new[] { 10, ElementId.NonCommittedId, 11 }, ids);
    Assert.Equal(new[] { DataCategory.FamilyAttachment }, categories);
    Assert.Equal("image/jpeg", info.Attachments[1].MimeType);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task A_family_image_attachments_downsized_thumbnail_opens_its_full_size_image_and_a_documents_nothing()
  {
    var services = new TestServices();
    var scan = new Data(
      10, GedcomPhotoResidue.EncodeAttachment(TestImages.Png(300, 200), "deed.png"), "image/png", DataCategory.FamilyAttachment);
    var dialog = await CreateDialogAsync(services, [scan, Attachment(11)]);
    await using var window = await WindowHost.AttachAsync(dialog);
    var previews = await MediaPreviews.WaitAsync(dialog, dialog.Attachments);
    var image = dialog.Attachments.First();
    var document = dialog.Attachments.Last();
    var thumbnail = await WaitForAsync(() => image.Thumbnail, source => source is not null, "The thumbnail never resolved.");
    await WaitForAsync(() => document.Content, content => content is not null, "The document never loaded.");

    await MediaPreviews.TapAsync(dialog, dialog.Attachments, document);
    await Poll.ConfirmNeverAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog.Navigation.ModalStack.OfType<PhotoViewerDialog>().Any()),
      open => open,
      TimeSpan.FromSeconds(1),
      "A document's row opened the viewer.");
    await MediaPreviews.TapAsync(dialog, dialog.Attachments, image);
    var viewer = await ModalDialogHarness.WaitForModalAsync<PhotoViewerDialog>(dialog);

    Assert.Equal(new Size(100, 67), ImageUtils.PixelSize(await PhotoBytes.ReadAsync(thumbnail!)));
    Assert.False(await MainThread.InvokeOnMainThreadAsync(() => document.HasThumbnail));
    Assert.Same(image.Image, Assert.Single(viewer.Photos));
    var rowTaps = await MainThread.InvokeOnMainThreadAsync(
      () => previews.SelectMany(preview => ((View)preview.Owner.Parent).GestureRecognizers.OfType<TapGestureRecognizer>()).ToArray());
    Assert.Empty(rowTaps);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
    await MainThread.InvokeOnMainThreadAsync(() => viewer.CloseCommand.Execute(null));
  }

  [Fact]
  public async Task Tapping_a_family_photo_opens_only_that_photo_in_the_viewer()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [
      new Data(10, TestImages.Png(4, 3), "image/png", DataCategory.FamilyMainPhoto),
      new Data(11, TestImages.Png(5, 2), "image/png", DataCategory.FamilyPhoto)]);
    await using var window = await WindowHost.AttachAsync(dialog);
    var photo = dialog.Photos.Last();
    var image = await WaitForAsync(() => photo.Image, source => source is not null, "The photo never loaded.");

    await MediaPreviews.TapAsync(dialog, dialog.Photos, photo);
    var viewer = await ModalDialogHarness.WaitForModalAsync<PhotoViewerDialog>(dialog);

    Assert.Same(image, Assert.Single(viewer.Photos));
    await MainThread.InvokeOnMainThreadAsync(() => viewer.CloseCommand.Execute(null));
  }

  [Fact]
  public async Task Enhancement_models_stay_loaded_until_the_dialog_is_saved()
  {
    var services = new TestServices();
    var enhancement = new DoublingEnhancement();
    var enhancer = new TestablePhotoEnhancer(services, [enhancement]) { EnhancementAnswer = enhancement.DisplayName };
    var dialog = await CreateDialogAsync(services, [new Data(10, TestImages.ValidPng, "image/png", DataCategory.FamilyMainPhoto)], enhancer);
    await using var window = await WindowHost.AttachAsync(dialog);

    await EnhanceAdorners.TapAsync(dialog, dialog.Photos, "EnhanceFamilyPhotoCommand", dialog.Photos.Single());
    await WaitForAsync(() => dialog.Photos.Count, count => count == 2, "The enhanced photo was never added.");
    Assert.Equal(0, enhancement.ReleaseCount);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    await dialog.Info;

    Assert.Equal(1, enhancement.ReleaseCount);
  }

  [Fact]
  public async Task Cancelling_the_dialog_releases_the_enhancement_models()
  {
    var services = new TestServices();
    var enhancement = new DoublingEnhancement();
    var dialog = await CreateDialogAsync(services, photoEnhancer: new TestablePhotoEnhancer(services, [enhancement]));

    await MainThread.InvokeOnMainThreadAsync(() => dialog.CancelCommand.Execute(null));

    Assert.Null(await dialog.Info);
    Assert.Equal(1, enhancement.ReleaseCount);
  }

  [Fact]
  public async Task Closing_a_persons_own_name_dialog_leaves_the_enhancement_models_loaded()
  {
    var services = new TestServices();
    var enhancement = new DoublingEnhancement();
    var enhancer = new TestablePhotoEnhancer(services, [enhancement]);
    var dialog = await CreateDialogForTypeAsync(services, NameType.FirstName | NameType.MaleDeclension, enhancer);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.CancelCommand.Execute(null));

    Assert.Null(await dialog.Info);
    Assert.Equal(0, enhancement.ReleaseCount);
  }

  [Fact]
  public async Task A_long_photo_caption_does_not_widen_the_photo_slot()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto)]);
    var photo = dialog.Photos.Single();
    await MainThread.InvokeOnMainThreadAsync(
      () => photo.Caption = "The family at the old house in the summer of 1952 (AI: colorized) (AI: restored) (AI: upscaled x4)");

    await using var window = await WindowHost.AttachAsync(dialog);
    var (slot, picture) = await CaptionRows.PhotoSlotWidthsAsync(dialog, photo);

    Assert.Equal(picture, slot, 1.0);
  }

  [Fact]
  public async Task A_long_attachment_caption_stays_within_its_row()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Attachment(1)]);
    var attachment = dialog.Attachments.Single();
    var sentences = Enumerable.Repeat("A deed to the family farm from the county records office. ", 8);
    await MainThread.InvokeOnMainThreadAsync(() => attachment.Caption = string.Concat(sentences));

    await using var window = await WindowHost.AttachAsync(dialog);
    var (row, captionRight) = await CaptionRows.CaptionExtentAsync(dialog, attachment);

    Assert.InRange(captionRight, 0, row + 1);
  }

  [Fact]
  public async Task No_enhance_adorner_shows_when_no_enhancement_is_registered()
  {
    var dialog = await CreateDialogAsync(new TestServices(), [Photo(1, DataCategory.FamilyMainPhoto), Photo(2)]);
    await using var window = await WindowHost.AttachAsync(dialog);

    var adorners = await EnhanceAdorners.WaitAsync(dialog, dialog.Photos, "EnhanceFamilyPhotoCommand");

    await MainThread.InvokeOnMainThreadAsync(() => Assert.All(adorners, adorner => Assert.False(adorner.IsVisible)));
  }
}
