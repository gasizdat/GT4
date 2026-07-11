using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Converters;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers PersonDataItem.ToDataAsync's two correctness-sensitive branches: an untouched item must be
/// returned as-is (no reconversion -- for a tagged photo, reconverting would silently regenerate
/// Content as plain bytes while Category still said tagged), and a genuinely modified item must
/// downgrade a tagged category to plain (DataCategoryExtensions.AsPlainPhoto), since a freshly
/// converted result can never legitimately carry the old photo's tags.
/// </summary>
public class PersonDataItemTests
{
  private static ICancellationTokenProvider TokenProvider(TestServices services) =>
    services.Provider.GetRequiredService<ICancellationTokenProvider>();

  [Fact]
  public async Task ToDataAsync_UntouchedItem_ReturnsTheOriginalDataUnchanged()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(), TokenProvider(services), services.AlertService.Object);

    var result = await item.ToDataAsync();

    Assert.Same(original, result);
  }

  [Fact]
  public async Task ToDataAsync_ModifiedTaggedItem_DowngradesToPlainCategory()
  {
    var services = new TestServices();
    var original = new Data(10, [1, 2, 3], "image/png", DataCategory.PersonMainPhotoTagged);
    var item = new PersonDataItem(original, new PhotoTagDataConverter(), TokenProvider(services), services.AlertService.Object);

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
    var item = new PersonDataItem(original, new ImageDataConverter(), TokenProvider(services), services.AlertService.Object);

    item.Content = ImageUtils.ImageFromBytes([9, 9, 9]);
    var result = await item.ToDataAsync();

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonPhoto, result.Category);
  }
}
