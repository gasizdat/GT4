using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Pages;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers GalleryPage against a real MAUI runtime with a mocked Core. The page joins the Data-table
/// sweep with the two reverse lookups in memory, so what is pinned here is that join -- which rows
/// survive it, which owners each row ends up carrying, and what a row is captioned with.
/// </summary>
public class GalleryPageTests
{
  // An item's icon resolves through an ImageDataWithMaxSize, which decodes and re-encodes the source
  // bytes to build a thumbnail, so anything a platform decoder rejects would never resolve.
  private static readonly byte[] ValidPngBytes = TestImages.ValidPng;

  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName) =>
    new(id, default(Date), null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  private static Data Photo(int id, DataCategory category) => new(id, ValidPngBytes, "image/png", category);

  private static Data Attachment(int id, string fileName) =>
    new(id, GedcomPhotoResidue.EncodeAttachment(ValidPngBytes, fileName), "application/pdf", DataCategory.PersonAttachment);

  private static async Task<TestableGalleryPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableGalleryPage>());
  }

  private static Task<GalleryDataItem[]> WaitForItemsAsync(GalleryPage page, int expectedCount) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.Items.ToArray()),
      items => items.Length == expectedCount,
      timeoutMessage: $"The gallery did not settle on {expectedCount} item(s); check the TestServices mock setup.");

  private static void SetUpProject(
    TestServices services,
    Data[] dataSet,
    PersonInfo[]? persons = null,
    Name[]? names = null,
    Dictionary<int, int[]>? personIdsByData = null,
    Dictionary<int, int[]>? nameIdsByData = null)
  {
    services.Data
      .Setup(d => d.GetDataSetAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(dataSet);
    services.PersonManager
      .Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>()))
      .ReturnsAsync(persons ?? []);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(NameType.AllNames, It.IsAny<CancellationToken>()))
      .ReturnsAsync(names ?? []);
    services.PersonData
      .Setup(p => p.GetPersonIdsByDataAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(personIdsByData ?? []);
    services.NameData
      .Setup(n => n.GetNameIdsByDataAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(nameIdsByData ?? []);
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.Equal(string.Empty, page.OwnerFilter);
    Assert.NotNull(page.DeleteDataCommand);
    Assert.NotNull(page.OpenDataCommand);
  }

  [Fact]
  public async Task Every_data_row_is_listed_once_carrying_all_of_its_owners()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var petr = P(2, "Petr");
    var ivanov = N(9, "Ivanov", NameType.FamilyName);
    SetUpProject(
      services,
      dataSet: [Photo(10, DataCategory.PersonPhoto), Photo(11, DataCategory.FamilyMainPhoto)],
      persons: [ivan, petr],
      names: [ivanov],
      personIdsByData: new() { [10] = [ivan.Id, petr.Id] },
      nameIdsByData: new() { [10] = [ivanov.Id], [11] = [ivanov.Id] });
    var page = await CreatePageAsync(services);
    var nameFormatter = services.Provider.GetRequiredService<INameFormatter>();

    var items = await WaitForItemsAsync(page, 2);

    var ivanName = nameFormatter.ToString(ivan, NameFormat.CommonPersonName);
    var petrName = nameFormatter.ToString(petr, NameFormat.CommonPersonName);
    var shared = items.Single(item => item.Info.Id == 10);
    Assert.Equal([ivanName, petrName], shared.PersonNames);
    Assert.Equal(["Ivanov"], shared.FamilyNames);
    Assert.Equal($"{ivanName}, {petrName}, Ivanov", shared.Owners);
    Assert.Equal(["Ivanov"], items.Single(item => item.Info.Id == 11).FamilyNames);
  }

  [Fact]
  public async Task Data_no_owner_links_is_still_listed()
  {
    var services = new TestServices();
    SetUpProject(services, dataSet: [Photo(10, DataCategory.PersonPhoto)]);
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 1);

    var orphan = items.Single();
    Assert.Empty(orphan.Persons);
    Assert.Empty(orphan.Families);
    Assert.False(orphan.HasOwners);
  }

  [Fact]
  public async Task Data_that_is_neither_a_photo_nor_an_attachment_is_left_out()
  {
    var services = new TestServices();
    var biography = new Data(20, ValidPngBytes, "text/markdown", DataCategory.PersonBio);
    var gedcomTags = new Data(21, ValidPngBytes, null, DataCategory.PersonGedcomTags);
    SetUpProject(services, dataSet: [Photo(22, DataCategory.PersonPhoto), biography, gedcomTags]);
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 1);

    Assert.Equal(22, items.Single().Info.Id);
  }

  [Fact]
  public async Task An_attachment_is_titled_with_its_stored_file_name()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    SetUpProject(
      services,
      dataSet: [Attachment(30, "birth-certificate.pdf")],
      persons: [ivan],
      personIdsByData: new() { [30] = [ivan.Id] });
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 1);

    var title = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => items.Single().Title),
      title => title != items.Single().Owners,
      timeoutMessage: "The attachment kept its owner fallback instead of resolving its file name.");
    Assert.Equal("birth-certificate.pdf", title);
  }

  [Fact]
  public async Task A_photo_with_no_caption_is_titled_with_its_owners()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    SetUpProject(
      services,
      dataSet: [Photo(40, DataCategory.PersonPhoto)],
      persons: [ivan],
      personIdsByData: new() { [40] = [ivan.Id] });
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 1);
    var item = items.Single();

    // The conversion that could have supplied a caption still has to run and come back empty-handed,
    // so prove the fallback outlasts it rather than just reading Title before it resolves.
    await Poll.ConfirmNeverAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => item.Title),
      title => title != item.Owners,
      TimeSpan.FromSeconds(1),
      "An uncaptioned photo stopped falling back to its owner names.");
    Assert.Equal(item.Owners, item.Title);
  }

  // Both this page and SelectMediaDialog print Owners under the title, so an item whose title *is* its
  // owners must not claim they are worth a second line -- that row would show the same name twice.
  [Fact]
  public async Task Owners_are_distinct_from_the_title_only_once_a_caption_resolves()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    SetUpProject(
      services,
      dataSet: [Photo(40, DataCategory.PersonPhoto), Attachment(41, "birth-certificate.pdf")],
      persons: [ivan],
      personIdsByData: new() { [40] = [ivan.Id], [41] = [ivan.Id] });
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 2);
    var uncaptioned = items.Single(item => item.Info.Id == 40);
    var named = items.Single(item => item.Info.Id == 41);

    // Reading Title is what starts the conversion the flag depends on -- HasDistinctOwners alone never
    // asks for one, so polling it directly would spin against an item that was never told to resolve.
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => named.Title),
      title => title != named.Owners,
      timeoutMessage: "The attachment kept its owner fallback instead of resolving its file name.");
    Assert.True(named.HasDistinctOwners);

    await MainThread.InvokeOnMainThreadAsync(() => uncaptioned.Title);
    await Poll.ConfirmNeverAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => uncaptioned.HasDistinctOwners),
      distinct => distinct,
      TimeSpan.FromSeconds(1),
      "An uncaptioned photo claimed its owners were worth a line separate from its title.");
  }

  [Fact]
  public async Task Toggling_an_item_shows_and_hides_its_owners()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    SetUpProject(
      services,
      dataSet: [Photo(50, DataCategory.PersonPhoto)],
      persons: [ivan],
      personIdsByData: new() { [50] = [ivan.Id] });
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 1);
    var item = items.Single();

    Assert.False(item.ShowOwners);
    var collapsedBtnName = item.MoreBtnName;

    await MainThread.InvokeOnMainThreadAsync(() => item.ToggleOwnersCommand.Execute(null));
    Assert.True(item.ShowOwners);
    Assert.NotEqual(collapsedBtnName, item.MoreBtnName);

    await MainThread.InvokeOnMainThreadAsync(() => item.ToggleOwnersCommand.Execute(null));
    Assert.False(item.ShowOwners);
    Assert.Equal(collapsedBtnName, item.MoreBtnName);
  }

  [Fact]
  public async Task Deleting_an_item_drops_every_owner_link_and_the_row()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var ivanov = N(9, "Ivanov", NameType.FamilyName);
    var shared = Photo(60, DataCategory.PersonPhoto);
    SetUpProject(
      services,
      dataSet: [shared],
      persons: [ivan],
      names: [ivanov],
      personIdsByData: new() { [60] = [ivan.Id] },
      nameIdsByData: new() { [60] = [ivanov.Id] });
    var page = await CreatePageAsync(services);
    var items = await WaitForItemsAsync(page, 1);

    await page.InvokeDeleteAsync(items.Single());

    services.PersonData.Verify(p => p.RemovePersonDataAsync(ivan, shared, It.IsAny<CancellationToken>()), Times.Once());
    services.NameData.Verify(n => n.RemoveNameDataAsync(ivanov, shared, It.IsAny<CancellationToken>()), Times.Once());
    services.Data.Verify(d => d.RemoveDataAsync(shared, It.IsAny<CancellationToken>()), Times.Once());
    services.Transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task Deleting_an_unowned_item_still_drops_the_row()
  {
    var services = new TestServices();
    var orphan = Photo(70, DataCategory.PersonPhoto);
    SetUpProject(services, dataSet: [orphan]);
    var page = await CreatePageAsync(services);
    var items = await WaitForItemsAsync(page, 1);

    await page.InvokeDeleteAsync(items.Single());

    services.Data.Verify(d => d.RemoveDataAsync(orphan, It.IsAny<CancellationToken>()), Times.Once());
    services.PersonData.Verify(
      p => p.RemovePersonDataAsync(It.IsAny<Person>(), It.IsAny<Data>(), It.IsAny<CancellationToken>()),
      Times.Never());
  }

  // The attachment branch hands the file to Launcher.Default, which has no headless outcome to assert;
  // it is AttachmentInfo.OpenAsync, the same call PersonPage makes for a person's own attachments.
  [Fact]
  public async Task Opening_a_photo_shows_it_in_the_photo_viewer()
  {
    var services = new TestServices();
    SetUpProject(services, dataSet: [Photo(100, DataCategory.PersonPhoto)]);
    var page = await CreatePageAsync(services);
    var items = await WaitForItemsAsync(page, 1);

    await using var window = await WindowHost.AttachAsync(page);
    var openTask = await MainThreadTask.StartAsync(() => page.InvokeOpenAsync(items.Single()));
    var dialog = await ModalDialogHarness.WaitForModalAsync<PhotoViewerDialog>(page);
    await openTask;

    Assert.NotNull(dialog);
    await MainThread.InvokeOnMainThreadAsync(() => page.Navigation.PopModalAsync());
  }

  [Fact]
  public async Task OwnerFilter_matches_case_insensitively_without_reloading()
  {
    var services = new TestServices();
    var ivan = P(1, "Ivan");
    var ivanov = N(9, "Ivanov", NameType.FamilyName);
    SetUpProject(
      services,
      dataSet: [Photo(80, DataCategory.PersonPhoto), Photo(81, DataCategory.FamilyPhoto)],
      persons: [ivan],
      names: [ivanov],
      personIdsByData: new() { [80] = [ivan.Id] },
      nameIdsByData: new() { [81] = [ivanov.Id] });
    var page = await CreatePageAsync(services);
    await WaitForItemsAsync(page, 2);
    var callsBefore = services.Data.Invocations.Count;

    await MainThread.InvokeOnMainThreadAsync(() => page.OwnerFilter = "IVANOV");
    var filtered = await MainThread.InvokeOnMainThreadAsync(() => page.Items.ToArray());

    Assert.Equal(81, filtered.Single().Info.Id);
    Assert.Equal(callsBefore, services.Data.Invocations.Count);

    await MainThread.InvokeOnMainThreadAsync(() => page.OwnerFilter = string.Empty);
    var restored = await MainThread.InvokeOnMainThreadAsync(() => page.Items.ToArray());

    Assert.Equal(2, restored.Length);
  }

  [Fact]
  public async Task An_image_resolves_its_own_thumbnail_as_the_icon()
  {
    var services = new TestServices();
    SetUpProject(services, dataSet: [Photo(90, DataCategory.PersonPhoto), Attachment(91, "scan.pdf")]);
    var page = await CreatePageAsync(services);

    var items = await WaitForItemsAsync(page, 2);

    var photoItem = items.Single(item => item.Info.Id == 90);
    var documentItem = items.Single(item => item.Info.Id == 91);
    // A file that does not render as an image keeps the stub, which doubles as the baseline the
    // photo's resolved thumbnail has to differ from.
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
