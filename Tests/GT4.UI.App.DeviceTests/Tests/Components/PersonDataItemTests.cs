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
  private static ICancellationTokenProvider TokenProvider(TestServices services) =>
    services.Provider.GetRequiredService<ICancellationTokenProvider>();

  [Fact]
  public async Task ToDataAsync_UntouchedItem_ReturnsTheOriginalDataUnchanged()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()), TokenProvider(services), services.AlertService.Object);

    var result = await item.ToDataAsync();

    Assert.Same(original, result);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedTaggedItem_DowngradesToPlainCategory()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(Mock.Of<IHttpClientFactory>()), TokenProvider(services), services.AlertService.Object);

    item.Content = ImageUtils.ImageFromBytes([9, 9, 9]);
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonMainPhoto, result.Category);
    Assert.Equal(ElementId.NonCommittedId, result.Id);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedPlainItem_StaysPlain()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonPhoto);
    var item = new PersonDataItem(original, new ImageDataConverter(Mock.Of<IHttpClientFactory>()), TokenProvider(services), services.AlertService.Object);

    item.Content = ImageUtils.ImageFromBytes([9, 9, 9]);
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonPhoto, result.Category);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedNonPhotoItem_LeavesCategoryUntouched()
  {
    // AsPlainPhoto() throws for non-photo categories, so ToDataAsync must guard with IsPhoto()
    // before calling it -- this covers the branch that skips the guarded call entirely.
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "text/plain", DataCategory.PersonBio);
    var item = new PersonDataItem(original, new TextDataConverter(), TokenProvider(services), services.AlertService.Object);

    item.Content = "updated biography";
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonBio, result.Category);
  }
}
