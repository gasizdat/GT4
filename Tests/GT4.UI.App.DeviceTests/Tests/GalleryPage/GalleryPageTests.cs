using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Items;
using GT4.UI.Pages;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers GalleryPage against a real MAUI runtime with a mocked Core. The page has no query of its own:
/// it composes the project-wide listing in memory from the per-owner data sets, so what is pinned here
/// is that join -- which Data rows survive it, and which owner names each surviving row ends up with.
/// </summary>
public class GalleryPageTests
{
  // A minimal valid 1x1 PNG: an item's icon resolves through an ImageDataWithMaxSize, which decodes and
  // re-encodes the source bytes to build a thumbnail, so arbitrary non-image bytes would throw.
  private static readonly byte[] ValidPngBytes =
  [
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x05, 0xC4, 0x89, 0x00, 0x00, 0x00,
    0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0x64, 0xF8, 0x0F, 0x00,
    0x01, 0x05, 0x01, 0x27, 0x23, 0xE3, 0x66, 0x66, 0x00, 0x00, 0x00, 0x00,
    0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
  ];

  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName) =>
    new(id, default(Date), null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  private static Data Photo(int id, DataCategory category) => new(id, ValidPngBytes, "image/png", category);

  private static async Task<GalleryPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<GalleryPage>());
  }

  private static Task<GalleryDataItem[]> WaitForItemsAsync(GalleryPage page, int expectedCount) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.Items.ToArray()),
      items => items.Length == expectedCount,
      timeoutMessage: $"The gallery did not settle on {expectedCount} item(s); check the TestServices mock setup.");

  private static void SetUpProject(TestServices services, PersonInfo[] persons, Name[] families,
    Dictionary<int, Data[]> personData, Dictionary<int, Data[]> familyData)
  {
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync(persons);
    services.PersonData
      .Setup(p => p.GetPersonDataSetAsync(It.IsAny<Person[]>(), null, It.IsAny<CancellationToken>()))
      .ReturnsAsync(personData);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.FamilyName, It.IsAny<CancellationToken>()))
      .ReturnsAsync(families);
    services.NameData
      .Setup(n => n.GetNameDataSetAsync(It.IsAny<Name[]>(), null, It.IsAny<CancellationToken>()))
      .ReturnsAsync(familyData);
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.Equal(string.Empty, page.OwnerFilter);
    Assert.Empty(page.Items);
  }

  [Fact]
  public async Task Every_data_row_is_listed_once_carrying_all_of_its_owners()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var petr = P(2, "Petr");
    var ivanov = N(9, "Ivanov", NameType.FamilyName);
    var shared = Photo(10, DataCategory.PersonPhoto);
    SetUpProject(
      services,
      persons: [ivan, petr],
      families: [ivanov],
      personData: new() { [ivan.Id] = [shared, Photo(11, DataCategory.PersonMainPhoto)], [petr.Id] = [shared] },
      familyData: new() { [ivanov.Id] = [shared, Photo(12, DataCategory.FamilyMainPhoto)] });
    var page = await CreatePageAsync(services);
    var nameFormatter = services.Provider.GetRequiredService<INameFormatter>();

    var items = await WaitForItemsAsync(page, 3);

    var ivanName = nameFormatter.ToString(ivan, NameFormat.CommonPersonName);
    var petrName = nameFormatter.ToString(petr, NameFormat.CommonPersonName);
    var sharedItem = items.Single(item => item.Info.Id == shared.Id);
    Assert.Equal($"{ivanName}, {petrName}, Ivanov", sharedItem.Owners);
    Assert.Equal(ivanName, items.Single(item => item.Info.Id == 11).Owners);
    Assert.Equal("Ivanov", items.Single(item => item.Info.Id == 12).Owners);
  }

  [Fact]
  public async Task Data_that_is_neither_a_photo_nor_an_attachment_is_left_out()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var biography = new Data(20, ValidPngBytes, "text/markdown", DataCategory.PersonBio);
    var gedcomTags = new Data(21, ValidPngBytes, null, DataCategory.PersonGedcomTags);
    SetUpProject(
      services,
      persons: [ivan],
      families: [],
      personData: new() { [ivan.Id] = [Photo(22, DataCategory.PersonPhoto), biography, gedcomTags] },
      familyData: []);
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 1);

    Assert.Equal(22, items.Single().Info.Id);
  }

  [Fact]
  public async Task OwnerFilter_matches_case_insensitively_without_reloading()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    SetUpProject(
      services,
      persons: [ivan],
      families: [N(9, "Ivanov", NameType.FamilyName)],
      personData: new() { [ivan.Id] = [Photo(30, DataCategory.PersonPhoto)] },
      familyData: new() { [9] = [Photo(31, DataCategory.FamilyPhoto)] });
    var page = await CreatePageAsync(services);
    await WaitForItemsAsync(page, 2);
    var callsBefore = services.PersonData.Invocations.Count;

    await MainThread.InvokeOnMainThreadAsync(() => page.OwnerFilter = "IVANOV");
    var filtered = await MainThread.InvokeOnMainThreadAsync(() => page.Items.ToArray());

    Assert.Equal(31, filtered.Single().Info.Id);
    Assert.Equal(callsBefore, services.PersonData.Invocations.Count);

    await MainThread.InvokeOnMainThreadAsync(() => page.OwnerFilter = string.Empty);
    var restored = await MainThread.InvokeOnMainThreadAsync(() => page.Items.ToArray());

    Assert.Equal(2, restored.Length);
  }

  [Fact]
  public async Task An_image_resolves_its_own_thumbnail_while_another_file_keeps_the_stub()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var document = new Data(41, ValidPngBytes, "application/pdf", DataCategory.PersonAttachment);
    SetUpProject(
      services,
      persons: [ivan],
      families: [],
      personData: new() { [ivan.Id] = [Photo(40, DataCategory.PersonPhoto), document] },
      familyData: []);
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 2);

    var photoItem = items.Single(item => item.Info.Id == 40);
    var documentItem = items.Single(item => item.Info.Id == document.Id);
    // A file that does not render as an image never reaches a converter, so its icon stays the stub --
    // which doubles as the baseline the photo's resolved thumbnail has to differ from.
    var stubBytes = await PhotoBytes.ReadAsync(documentItem.Icon);
    var iconBytes = await Poll.UntilAsync(
      () => PhotoBytes.ReadAsync(photoItem.Icon),
      bytes => !bytes.SequenceEqual(stubBytes),
      timeoutMessage: "The gallery icon did not resolve to the item's own photo.");

    // A 1x1 source is already far under ThumbnailSize, so the thumbnail stays 1x1 -- distinguishing it
    // from the ~100px stub confirms the resolved icon really is that item's photo.
    Assert.Equal(new Size(1, 1), ImageUtils.PixelSize(iconBytes));
    Assert.Equal(stubBytes, await PhotoBytes.ReadAsync(documentItem.Icon));
  }
}
