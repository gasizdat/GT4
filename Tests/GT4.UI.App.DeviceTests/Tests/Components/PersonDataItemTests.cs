using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Converters;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>Covers PersonDataItem.ToDataAsync's untouched-vs-modified and tagged-vs-plain branches.</summary>
public class PersonDataItemTests
{
  private const int CacheSizeLimit = 1024 * 1024;

  private static ICancellationTokenProvider TokenProvider(TestServices services) =>
    services.Provider.GetRequiredService<ICancellationTokenProvider>();

  [Fact]
  public async Task ToDataAsync_UntouchedItem_ReturnsTheOriginalDataUnchanged()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    var result = await item.ToDataAsync();

    Assert.Same(original, result);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedTaggedItem_DowngradesToPlainCategory()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Content = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([9, 9, 9])), null);
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonMainPhoto, result.Category);
    Assert.Equal(ElementId.NonCommittedId, result.Id);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedTaggedItemWithCaption_StaysTagged()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Content = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([9, 9, 9])), "A caption");
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    // Main-vs-additional is the item's to keep; the converter only decides tagged-vs-plain.
    Assert.Equal(DataCategory.PersonMainPhotoTagged, result.Category);
    Assert.Equal("A caption", await GedcomPhotoResidue.ExtractTitleAsync(result, CancellationToken.None));
  }

  [Fact]
  public async Task ToDataAsync_ModifiedPlainItem_StaysPlain()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonPhoto);
    var item = new PersonDataItem(original, new ImageDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Content = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([9, 9, 9])), null);
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonPhoto, result.Category);
  }

  [Fact]
  public async Task ToDataAsync_CaptionSetOnAPlainPhoto_PromotesToTaggedWithoutTouchingImageBytes()
  {
    var services = new TestServices();
    byte[] imageBytes = [1, 2, 3];
    var original = new Data(10, imageBytes, "image/png", DataCategory.PersonPhoto);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Caption = "A caption";
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonPhotoTagged, result.Category);
    Assert.Equal(ElementId.NonCommittedId, result.Id);
    Assert.Equal(imageBytes, GedcomPhotoResidue.ExtractImageBytes(result.Content));
    Assert.Equal("A caption", await GedcomPhotoResidue.ExtractTitleAsync(result, CancellationToken.None));
  }

  [Fact]
  public async Task ToDataAsync_WhitespaceOnlyCaptionSetOnAPlainPhoto_StaysPlain()
  {
    // Whitespace-only counts as no caption. This also pins that ToDataAsync branches on _CaptionModified
    // (was the item touched), not on whether the value is blank -- a real caption must still save.
    var services = new TestServices();
    byte[] imageBytes = [1, 2, 3];
    var original = new Data(10, imageBytes, "image/png", DataCategory.PersonPhoto);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Caption = "   ";
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonPhoto, result.Category);
    Assert.Equal(imageBytes, result.Content);
  }

  [Fact]
  public async Task ToDataAsync_CaptionClearedOnATaggedPhoto_DemotesToPlainWithOriginalImageBytes()
  {
    var services = new TestServices();
    byte[] imageBytes = [4, 5, 6];
    var content = GedcomPhotoResidue.EncodePhotoTitle(imageBytes, "Old caption");
    var original = new Data(10, content, "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Caption = null;
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonMainPhoto, result.Category);
    Assert.Equal(imageBytes, result.Content);
  }

  [Fact]
  public async Task ToDataAsync_CaptionSetOnAnAttachment_KeepsFileNameAndEnvelope()
  {
    var services = new TestServices();
    byte[] fileBytes = [9, 8, 7];
    var content = GedcomPhotoResidue.EncodeAttachment(fileBytes, "deed.pdf");
    var original = new Data(10, content, "application/pdf", DataCategory.PersonAttachment);
    var item = new PersonDataItem(original, new AttachmentDataConverter(DataCategory.PersonAttachment, Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Caption = "Estate deed";
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonAttachment, result.Category);
    Assert.Equal(fileBytes, GedcomPhotoResidue.ExtractImageBytes(result.Content));
    Assert.Equal("deed.pdf", await GedcomPhotoResidue.ExtractFileNameAsync(result, CancellationToken.None));
    Assert.Equal("Estate deed", await GedcomPhotoResidue.ExtractTitleAsync(result, CancellationToken.None));
  }

  [Fact]
  public void Caption_Get_FallsBackToDecodedContentCaption()
  {
    var services = new TestServices();
    var original = new Data(10, [], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);

    item.Content = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([1])), "Decoded caption");

    Assert.Equal("Decoded caption", item.Caption);
  }

  [Fact]
  public void Caption_Set_OverridesTheDecodedContentCaptionAndMarksTheItemModified()
  {
    var services = new TestServices();
    var original = new Data(10, [], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);
    item.Content = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([1])), "Decoded caption");

    item.Caption = "Typed caption";

    Assert.Equal("Typed caption", item.Caption);
    Assert.True(item.IsModified);
  }

  [Fact]
  public async Task Caption_EchoedBackFromTheBackgroundDecodesPropertyChanged_DoesNotMarkTheItemModified()
  {
    // Reproduces a bound Entry's TwoWay-binding echo: once Content's background decode raises
    // PropertyChanged(Caption), a WinUI Entry can push the identical text straight back into this
    // setter (its native TextBox fires TextChanged on a programmatic write, and MAUI's binding treats
    // that as user input). This must not register as a real edit.
    var services = new TestServices();
    byte[] imageBytes = [4, 5, 6];
    var content = GedcomPhotoResidue.EncodePhotoTitle(imageBytes, "Decoded caption");
    var original = new Data(10, content, "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit)), TokenProvider(services), services.AlertService.Object);
    item.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(PersonDataItem.Caption))
        item.Caption = item.Caption;
    };

    _ = item.Content;
    await Poll.UntilAsync(() => MainThread.InvokeOnMainThreadAsync(() => item.Content), c => c is not null, timeoutMessage: "Photo content never finished loading.");

    Assert.False(item.IsModified);
    Assert.Equal("Decoded caption", item.Caption);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedNonPhotoItem_LeavesCategoryUntouched()
  {
    // AsPlainPhoto() throws for non-photo categories, so ToDataAsync only reaches it behind an
    // IsTaggedPhoto() test -- this covers the branch that skips the guarded call entirely.
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "text/plain", DataCategory.PersonBio);
    var item = new PersonDataItem(original, new TextDataConverter(), TokenProvider(services), services.AlertService.Object);

    item.Content = "updated biography";
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonBio, result.Category);
  }
}
