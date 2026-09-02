using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Pages;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers PersonPage against a real MAUI runtime with a mocked Core. The relatives-tree rendering
/// itself is RelativeTree's own concern (see project_familytree/relatives-flat-tree memory), so
/// these tests assert scalar state (FullName, BirthDate, Photos) rather than Relatives rows.
/// PageCommand("EditPerson") pushes a modal via Navigation, driven through WindowHost/
/// ModalDialogHarness; CreateOrUpdatePersonDialog's own branches are covered by
/// CreateOrUpdatePersonDialogTests.
/// </summary>
public class PersonPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonFullInfo CreateSamplePerson() => PersonFullInfo.Empty with
  {
    Id = 1,
    BiologicalSex = BiologicalSex.Male,
    Names = [N(1, "Ivanov", NameType.FamilyName), N(2, "Ivan", NameType.FirstName | NameType.MaleDeclension)]
  };

  private static async Task<TestablePersonPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestablePersonPage>());
  }

  private static Task WaitForLoadAsync(TestablePersonPage page, TestServices services, Action interact) =>
    LoadWait.UntilAsync(() => page.CompletedLoads, services, interact, "Person");

  // Matches the [4-byte tag-length][UTF-8 GEDCOM tag text][image bytes] layout GedcomPhotoResidue
  // encodes; hand-built here since Encode itself isn't visible outside Core.Gedcom.
  private static byte[] BuildTaggedPhotoContent(string tagText, byte[] imageBytes)
  {
    var tagBytes = System.Text.Encoding.UTF8.GetBytes(tagText);
    using var buffer = new MemoryStream();
    buffer.Write(BitConverter.GetBytes(tagBytes.Length));
    buffer.Write(tagBytes);
    buffer.Write(imageBytes);
    return buffer.ToArray();
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.NotNull(page.PageCommand);
    Assert.False(page.ExpandAll);
    Assert.Empty(page.Photos);
  }

  [Fact]
  public async Task Menu_items_render_as_buttons_and_the_ToggleAll_caption_repaints_on_ExpandAll()
  {
    var page = await CreatePageAsync(new TestServices());
    var layout = (PageLayout)page.Content;
    await MainThread.InvokeOnMainThreadAsync(() => ((IView)layout).Arrange(new Rect(0, 0, 400, 800)));
    var topMenu = layout.FindByName<FlexLayout>("TopMenu");
    var buttons = topMenu.Children.OfType<Button>().ToArray();
    Assert.Equal(6, buttons.Length);

    var toggleButton = buttons.Single(b => (string)((PageMenuItem)b.BindingContext!).CommandParameter == "ToggleAll");
    Assert.Equal("⏬", toggleButton.Text);

    await MainThread.InvokeOnMainThreadAsync(() => page.ExpandAll = true);

    Assert.Equal("⏫", toggleButton.Text);
  }

  [Fact]
  public async Task ToggleFilters_menu_item_is_bound_to_the_page_command()
  {
    var page = await CreatePageAsync(new TestServices());

    var layout = (PageLayout)page.Content;
    var filterItem = layout.MenuItems.Single(item => (string?)item.CommandParameter == "ToggleFiltersCommand");

    Assert.NotNull(filterItem.Command);
    Assert.Same(page.FilterView.FilterCommand, filterItem.Command);
  }

  [Fact]
  public async Task ToggleFilters_command_shows_and_hides_the_filters_panel()
  {
    var page = await CreatePageAsync(new TestServices());
    await using var window = await WindowHost.AttachAsync(page);
    Assert.False(page.FilterView.IsFiltersVisible);

    var layout = (PageLayout)page.Content;
    var filterItem = layout.MenuItems.Single(item => (string?)item.CommandParameter == "ToggleFiltersCommand");

    // The flip cascades into FadeVisibilityBehavior touching native UI, so it must run on the UI
    // thread.
    await MainThread.InvokeOnMainThreadAsync(() => filterItem.Command.Execute(filterItem.CommandParameter));
    Assert.True(page.FilterView.IsFiltersVisible);

    await MainThread.InvokeOnMainThreadAsync(() => filterItem.Command.Execute(filterItem.CommandParameter));
    Assert.False(page.FilterView.IsFiltersVisible);
  }

  [Fact]
  public async Task Loading_a_person_populates_scalar_state()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    Assert.Equal(person.Id, page.PersonFullInfo.Id);
    Assert.NotEmpty(page.Photos);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task The_first_load_shows_the_indicator_until_the_person_arrives()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    var gate = new TaskCompletionSource<PersonFullInfo>();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .Returns(() => gate.Task);
    var page = await CreatePageAsync(services);

    var isLoading = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.PersonInfo = person;
      return page.Loading.IsLoading;
    });

    Assert.True(isLoading);
    gate.SetResult(person);
    await page.Loading.UntilIdleAsync("The indicator stayed on after the first person load landed.");
  }

  [Fact]
  public async Task A_refresh_over_a_loaded_person_never_shows_the_indicator()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    // Held open so the assertion reads the flag while the refresh is genuinely in flight.
    var gate = new TaskCompletionSource<PersonFullInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .Returns(() => gate.Task);

    var isLoading = await MainThread.InvokeOnMainThreadAsync(async () =>
    {
      await page.InvokePageCommandAsync("Refresh");
      return page.Loading.IsLoading;
    });

    Assert.False(isLoading);
    await WaitForLoadAsync(page, services, () => gate.SetResult(person));
  }

  [Fact]
  public async Task OnNavigatedTo_refetches_the_current_person_when_the_project_revision_changed()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var loadsBefore = page.CompletedLoads;
    services.CurrentProjectProvider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 1 });

    await WaitForLoadAsync(page, services, page.InvokeNavigatedTo);

    Assert.True(page.CompletedLoads > loadsBefore);
  }

  [Fact]
  public async Task OnNavigatedTo_does_not_refetch_when_the_project_revision_is_unchanged()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var loadsBefore = page.CompletedLoads;

    await MainThread.InvokeOnMainThreadAsync(page.InvokeNavigatedTo);
    await Task.Delay(200);

    Assert.Equal(loadsBefore, page.CompletedLoads);
  }

  [Fact]
  public async Task Loading_a_person_with_a_tagged_main_photo_surfaces_its_caption()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [1, 2, 3]);
    var mainPhoto = new Data(10, content, "image/png", DataCategory.PersonMainPhotoTagged);
    var person = CreateSamplePerson() with { MainPhoto = mainPhoto };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    var photo = Assert.Single(page.Photos);
    Assert.Equal("A caption", photo.Caption);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task Media_a_spouse_shares_is_linked_from_the_family_details_block()
  {
    // The couple's own row -- one Data linked to both spouses -- is the only handle on what their family
    // record carried, and the block must appear on it alone: this couple has no preserved FAM residue.
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE wedding.jpg\n1 TITL Their wedding\n", [1, 2, 3]);
    var wedding = new Data(30, content, "image/jpeg", DataCategory.PersonAttachment);
    var spouse = new PersonInfo(2, default(Date), null, BiologicalSex.Female, [N(3, "Anna", NameType.FirstName)], null);
    var person = CreateSamplePerson() with
    {
      Attachments = [wedding],
      RelativeInfos = [new RelativeInfo(spouse, RelationshipType.Spouse, null, Generation.Zero, Consanguinity.Zero)]
    };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    services.PersonData
      .Setup(p => p.GetPersonDataSetAsync(It.IsAny<Person[]>(), DataCategory.PersonAttachment, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, Data[]> { [2] = [wedding] });
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    Assert.Contains("[Their wedding](attachment:30)", page.Biography);
    Assert.Contains("(person:2)", page.Biography);
  }

  [Fact]
  public async Task Multiple_marriages_to_the_same_spouse_collapse_into_one_family_details_block()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE wedding.jpg\n1 TITL Their wedding\n", [1, 2, 3]);
    var wedding = new Data(30, content, "image/jpeg", DataCategory.PersonAttachment);
    var spouse = new PersonInfo(2, default(Date), null, BiologicalSex.Female, [N(3, "Anna", NameType.FirstName)], null);
    var firstMarriage = Date.Create(1990, 1, 1, DateStatus.WellKnown);
    var secondMarriage = Date.Create(1995, 6, 1, DateStatus.WellKnown);
    var person = CreateSamplePerson() with
    {
      Attachments = [wedding],
      RelativeInfos =
      [
        new RelativeInfo(spouse, RelationshipType.Spouse, firstMarriage, Generation.Zero, Consanguinity.Zero),
        new RelativeInfo(spouse, RelationshipType.Spouse, secondMarriage, Generation.Zero, Consanguinity.Zero)
      ]
    };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    services.PersonData
      .Setup(p => p.GetPersonDataSetAsync(It.IsAny<Person[]>(), DataCategory.PersonAttachment, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, Data[]> { [2] = [wedding] });
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    var occurrences = page.Biography.Split("(person:2)").Length - 1;
    Assert.Equal(1, occurrences);
    Assert.Contains("[Their wedding](attachment:30)", page.Biography);
  }

  [Fact]
  public async Task Media_the_spouse_does_not_carry_is_left_out_of_the_family_details_block()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE deed.pdf\n1 TITL A deed\n", [1, 2, 3]);
    var deed = new Data(31, content, "application/pdf", DataCategory.PersonAttachment);
    var spouse = new PersonInfo(2, default(Date), null, BiologicalSex.Female, [N(3, "Anna", NameType.FirstName)], null);
    var person = CreateSamplePerson() with
    {
      Attachments = [deed],
      RelativeInfos = [new RelativeInfo(spouse, RelationshipType.Spouse, null, Generation.Zero, Consanguinity.Zero)]
    };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    Assert.DoesNotContain("attachment:31", page.Biography);
  }

  [Fact]
  public async Task MediaResolver_resolves_a_main_photo_to_its_unwrapped_image()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [1, 2, 3]);
    var mainPhoto = new Data(10, content, "image/png", DataCategory.PersonMainPhotoTagged);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(10, It.IsAny<CancellationToken>()))
      .ReturnsAsync(mainPhoto);
    var page = await CreatePageAsync(services);

    var media = await page.MediaResolver("media:10");

    Assert.NotNull(media);
    Assert.Equal(new byte[] { 1, 2, 3 }, await PhotoBytes.ReadAsync(media.Source));
  }

  [Fact]
  public async Task MediaResolver_resolves_an_image_attachment_but_not_a_document_one()
  {
    var services = new TestServices();
    var scanContent = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.jpg\n", [4, 5, 6]);
    var scan = new Data(22, scanContent, "image/jpeg", DataCategory.PersonAttachment);
    var deedContent = BuildTaggedPhotoContent("0 OBJE\n1 FILE deed.pdf\n", [1, 2, 3]);
    var deed = new Data(23, deedContent, "application/pdf", DataCategory.PersonAttachment);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(22, It.IsAny<CancellationToken>()))
      .ReturnsAsync(scan);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(23, It.IsAny<CancellationToken>()))
      .ReturnsAsync(deed);
    var page = await CreatePageAsync(services);

    var scanMedia = await page.MediaResolver("media:22");
    var deedMedia = await page.MediaResolver("media:23");

    Assert.NotNull(scanMedia);
    Assert.Equal(new byte[] { 4, 5, 6 }, await PhotoBytes.ReadAsync(scanMedia.Source));
    Assert.Null(deedMedia);
  }

  [Fact]
  public async Task Loading_a_person_with_a_titled_attachment_shows_the_title_over_the_file_name()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE actes/scan.pdf\n1 TITL Acte de Bapteme\n", [1, 2, 3]);
    var attachment = new Data(20, content, "application/pdf", DataCategory.PersonAttachment);
    var person = CreateSamplePerson() with { Attachments = [attachment] };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    var info = Assert.Single(page.Attachments);
    Assert.Equal("Acte de Bapteme", info.Title);
    Assert.Equal("Acte de Bapteme", info.DisplayName);
    Assert.Equal("actes/scan.pdf", info.FileName);
    Assert.True(info.ShowFileName);
  }

  // The attachments tab is hidden for a person carrying none, so a page left standing on it would
  // show a blank body with no tab to leave by.
  [Fact]
  public async Task Opening_a_person_with_no_attachments_leaves_the_attachments_tab()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE deed.pdf\n", [1, 2, 3]);
    var attachment = new Data(22, content, "application/pdf", DataCategory.PersonAttachment);
    var documented = CreateSamplePerson() with { Attachments = [attachment] };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(documented);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = documented);
    await MainThread.InvokeOnMainThreadAsync(() => page.InvokePageCommandAsync("TabAttachments"));
    Assert.True(page.ShowAttachmentsTab);

    var undocumented = CreateSamplePerson() with { Id = 2 };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(undocumented);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = undocumented);

    Assert.True(page.ShowRelativesTab);
  }

  [Fact]
  public async Task Opening_a_person_with_no_biography_leaves_the_biography_tab()
  {
    var services = new TestServices();
    var written = CreateSamplePerson() with { Biography = LongBiography() };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(written);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = written);
    await MainThread.InvokeOnMainThreadAsync(() => page.InvokePageCommandAsync("TabBiography"));
    Assert.True(page.ShowBiographyTab);

    var unwritten = CreateSamplePerson() with { Id = 2 };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(unwritten);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = unwritten);

    Assert.True(page.ShowRelativesTab);
  }

  // A lone Relatives tab offers no choice, so the strip is only worth its space once something else
  // is there to switch to.
  [Fact]
  public async Task The_tab_strip_is_hidden_for_a_person_carrying_only_relatives()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    Assert.False(page.ShowTabs);
  }

  [Fact]
  public async Task The_tab_strip_appears_once_a_person_has_a_biography()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { Biography = LongBiography() };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    Assert.True(page.ShowTabs);
  }

  [Fact]
  public async Task Loading_a_person_with_an_untitled_attachment_falls_back_to_the_file_name_alone()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE deed.pdf\n", [1, 2, 3]);
    var attachment = new Data(21, content, "application/pdf", DataCategory.PersonAttachment);
    var person = CreateSamplePerson() with { Attachments = [attachment] };
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    var info = Assert.Single(page.Attachments);
    Assert.Null(info.Title);
    Assert.Equal("deed.pdf", info.DisplayName);
    Assert.False(info.ShowFileName);
  }

  [Fact]
  public async Task Opening_a_new_person_fetches_its_data_exactly_once()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    // AddToNavigation used to route a freshly-appended entry through the CurrentPerson setter,
    // re-triggering the whole fetch pipeline for the person that had just finished loading. Confirm
    // it settles at exactly one completed load, not two, over a grace window rather than a single
    // check right after the first load (a regression could land the second load slightly later).
    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(page.CompletedLoads),
      loads => loads > 1,
      TimeSpan.FromMilliseconds(300),
      failureMessage: "Opening a new person re-ran the fetch pipeline more than once.");
    Assert.Equal(1, page.CompletedLoads);
    services.PersonManager.Verify(
      p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task MaritalStatusData_is_not_fetched_until_the_filter_panel_is_shown()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(person);
    var page = await CreatePageAsync(services);

    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Never());

    await page.WaitForFilterDataAsync();
    services.Relatives.Verify(
      r => r.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()),
      Times.Once());
  }

  [Fact]
  public async Task Loading_reports_non_teardown_exceptions_and_navigates_back()
  {
    var services = new TestServices();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("DB error"));
    var page = await CreatePageAsync(services);

    var loadsBefore = page.CompletedLoads;
    await MainThread.InvokeOnMainThreadAsync(() => page.PersonInfo = CreateSamplePerson());
    await Poll.UntilAsync(
      () => Task.FromResult(services.AlertService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Load failure was not reported.");

    Assert.Equal(loadsBefore, page.CompletedLoads);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Once());
    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "Load failure did not navigate back.");
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task Loading_swallows_the_project_teardown_race()
  {
    var services = new TestServices();
    services.PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ObjectDisposedException(nameof(IProjectDocument)));
    var page = await CreatePageAsync(services);

    var loadsBefore = page.CompletedLoads;
    await MainThread.InvokeOnMainThreadAsync(() => page.PersonInfo = CreateSamplePerson());
    // Nothing observable ever happens on the teardown path (no load, no alert, no navigation) -- poll
    // over a grace window and fail as soon as any of them fire, instead of a single check after a
    // blind delay that a regression could slip past if it fires later than the delay on a loaded runner.
    await Poll.ConfirmNeverAsync(
      () => Task.FromResult(page.CompletedLoads != loadsBefore
        || services.AlertService.Invocations.Count > 0
        || services.NavigationService.Invocations.Count > 0),
      unwanted => unwanted,
      TimeSpan.FromMilliseconds(300),
      failureMessage: "The project-teardown race was not swallowed as expected.");

    Assert.Equal(loadsBefore, page.CompletedLoads);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
    services.NavigationService.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
  }

  [Fact]
  public async Task RemovePerson_removes_the_current_person_and_navigates_to_its_family()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var expectedRoute = $"{typeof(FamilyPage).Namespace}/{typeof(FamilyPage).Name}";
    var expectedFamilyName = person.Names.Single(n => n.Type == NameType.FamilyName);

    await page.InvokePageCommandAsync("RemovePerson");

    services.Persons.Verify(p => p.RemovePersonAsync(person, It.IsAny<CancellationToken>()), Times.Once());
    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => Equals(d["FamilyName"], expectedFamilyName))),
      Times.Once());
  }

  [Fact]
  public async Task RemovePerson_for_a_familyless_person_navigates_with_the_NoFamily_sentinel()
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { Names = [N(2, "Ivan", NameType.FirstName | NameType.MaleDeclension)] };
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var expectedRoute = $"{typeof(FamilyPage).Namespace}/{typeof(FamilyPage).Name}";

    await page.InvokePageCommandAsync("RemovePerson");

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        It.Is<Dictionary<string, object>>(d => Equals(d["FamilyName"], FamilyInfoItem.NoFamilyName))),
      Times.Once());
  }

  [Fact]
  public async Task EditPerson_modal_updates_the_person_and_reloads()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditPerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    // Wait for the reload the update triggers, not just the command: PersonInfo = info starts it
    // fire-and-forget, and leaving it in flight lets it race the window restore below into a
    // main-thread wedge that hangs the whole run (observed on CI).
    await WaitForLoadAsync(page, services, () =>
    {
      dialog.BirthDate = Date.Create(1990, 5, 1, DateStatus.WellKnown);
      dialog.DialogCommand.Execute("CreatePersonCommand");
    });
    await commandTask;

    services.PersonManager.Verify(p => p.UpdatePersonAsync(It.IsAny<PersonFullInfo>(), It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task EditPerson_does_not_duplicate_the_current_navigation_history_entry()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditPerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    await WaitForLoadAsync(page, services, () =>
    {
      dialog.BirthDate = Date.Create(1990, 5, 1, DateStatus.WellKnown);
      dialog.DialogCommand.Execute("CreatePersonCommand");
    });
    await commandTask;

    Assert.Equal(1, page.NavigationHistory.Count);
  }

  [Fact]
  public async Task EditPerson_cancelled_dialog_does_not_update()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await using var window = await WindowHost.AttachAsync(page);
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync("EditPerson"));
    var dialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdatePersonDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute("CreatePersonCommand"));
    await commandTask;

    services.PersonManager.Verify(p => p.UpdatePersonAsync(It.IsAny<PersonFullInfo>(), It.IsAny<CancellationToken>()), Times.Never());
  }


  // Far below the count that killed the host on the old layout (100 relatives did, intermittently):
  // an unbounded list is already unambiguous at 20 rows -- ~99px each against a 713px page -- so a
  // regression stays a red test rather than a dead run. ExpandAll walks the whole connected
  // relatives graph, so nothing bounds the real row count.
  private const int RelativesBelowCrashThreshold = 20;

  private static RelativeInfo Child(int id) => new(
    id, Date.Create(2015, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male,
    [N(id * 100, $"Child{id}", NameType.FirstName)], null,
    RelationshipType.Child, null, Generation.Child, Consanguinity.Zero);

  // ExpandAll opens the whole connected graph into one flat row list, so the count the view has to
  // hold is bounded by the project, not by how many relatives the person has. The walk itself is
  // RelativeTreeTests' concern; what PersonPage owes is surviving its result. On the old layout this
  // killed the host somewhere above 100 rows.
  [Fact]
  public async Task A_graph_sized_row_count_renders_without_crashing()
  {
    const int GraphSize = 2000;
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.RelativesProvider
      .Setup(r => r.GetChildren(It.IsAny<RelativeInfo[]>()))
      .Returns(Enumerable.Range(2, GraphSize).Select(Child).ToArray());
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    await MainThread.InvokeOnMainThreadAsync(() => page.ForceSizeAllocated(400, 800));
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    await MainThread.InvokeOnMainThreadAsync(() => page.ForceSizeAllocated(400, 800));

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.RelativesListForTest.Height),
      listHeight => listHeight > 0,
      timeoutMessage: "The relatives list never got arranged.");

    var (relativesHeight, pageHeight, rowCount) = await MainThread.InvokeOnMainThreadAsync(
      () => (page.RelativesListForTest.Height, page.Height, page.Relatives.Count));
    Assert.Equal(GraphSize, rowCount);
    Assert.True(
      relativesHeight <= pageHeight,
      $"The relatives list is {relativesHeight} tall inside a {pageHeight} page holding {rowCount} rows.");
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public Task Relatives_list_is_bounded_by_the_page_in_the_narrow_layout() =>
    AssertRelativesListIsBoundedAsync(400, 800);

  // The wide layout is where the photo sits beside the relatives, in the row that used to grow with
  // them.
  [Fact]
  public Task Relatives_list_is_bounded_by_the_page_in_the_wide_layout() =>
    AssertRelativesListIsBoundedAsync(2000, 800);

  [Fact]
  public Task A_long_biography_and_the_relatives_are_visible_together_in_the_narrow_layout() =>
    AssertRelativesListIsBoundedAsync(400, 800, withBiography: true);

  [Fact]
  public Task A_long_biography_and_the_relatives_are_visible_together_in_the_wide_layout() =>
    AssertRelativesListIsBoundedAsync(2000, 800, withBiography: true);

  // A long one: the biography shares the body with the relatives, and an unbounded one starves the
  // list to nothing instead of letting it scroll (see FamilyPage's Auto-row trap).
  private static Data LongBiography() => new(
    Id: 0,
    Content: System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Line of biography text.\n", 200))),
    MimeType: System.Net.Mime.MediaTypeNames.Text.Plain,
    Category: default);

  private static async Task AssertRelativesListIsBoundedAsync(double width, double height, bool withBiography = false)
  {
    var services = new TestServices();
    var person = withBiography
      ? CreateSamplePerson() with { Biography = LongBiography() }
      : CreateSamplePerson();
    var children = Enumerable.Range(2, RelativesBelowCrashThreshold).Select(Child).ToArray();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.RelativesProvider.Setup(r => r.GetChildren(It.IsAny<RelativeInfo[]>())).Returns(children);
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    // Forced again after loading -- attaching to the real window raises its own OnSizeAllocated off
    // the runner's actual size, overriding the forced one.
    await MainThread.InvokeOnMainThreadAsync(() => page.ForceSizeAllocated(width, height));
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    await MainThread.InvokeOnMainThreadAsync(() => page.ForceSizeAllocated(width, height));

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.RelativesListForTest.Height),
      listHeight => listHeight > 0,
      timeoutMessage: "The relatives list never got arranged.");

    var (relativesHeight, pageHeight, rowCount) = await MainThread.InvokeOnMainThreadAsync(
      () => (page.RelativesListForTest.Height, page.Height, page.Relatives.Count));
    Assert.Equal(RelativesBelowCrashThreshold, rowCount);
    AssertListFillsAViewport(relativesHeight, pageHeight, "");

    // The filter panel shares the body with the list, so showing it re-divides the space under a
    // list that has already been measured against the old division.
    await MainThread.InvokeOnMainThreadAsync(() => page.FilterView.IsFiltersVisible = true);
    var heightWithFilters = await MainThread.InvokeOnMainThreadAsync(() => page.RelativesListForTest.Height);
    AssertListFillsAViewport(heightWithFilters, pageHeight, " once the filter panel is shown");
  }

  // The window the user reported it broken in.
  [Fact]
  public Task Every_region_stays_inside_its_parent_in_a_short_window() =>
    AssertEveryRegionIsDrawnAsync(1423, 478);

  [Fact]
  public Task Every_region_stays_inside_its_parent_in_a_desktop_window() =>
    AssertEveryRegionIsDrawnAsync(1424, 713);

  [Fact]
  public Task Every_region_stays_inside_its_parent_in_a_narrow_window() =>
    AssertEveryRegionIsDrawnAsync(500, 900);

  // WinUI draws an Image at whatever height it was asked for: a picture bigger than the box it was
  // arranged into is not clipped, it is painted over the header above it, which is how a short
  // landscape window lost its dates and its tab strip.
  [Fact]
  public Task The_photo_stays_inside_its_box_in_a_short_landscape_window() =>
    AssertThePhotoStaysInsideItsBoxAsync(1030, 620);

  [Fact]
  public Task The_photo_stays_inside_its_box_in_a_very_short_landscape_window() =>
    AssertThePhotoStaysInsideItsBoxAsync(1424, 478);

  private static async Task AssertThePhotoStaysInsideItsBoxAsync(double windowWidth, double windowHeight)
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { Biography = LongBiography() };
    var children = Enumerable.Range(2, RelativesBelowCrashThreshold).Select(Child).ToArray();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.RelativesProvider.Setup(r => r.GetChildren(It.IsAny<RelativeInfo[]>())).Returns(children);
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var hostWindow = Application.Current!.Windows[0];
    var (originalWidth, originalHeight) = (hostWindow.Width, hostWindow.Height);
    try
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        hostWindow.Width = windowWidth;
        hostWindow.Height = windowHeight;
      });
      await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
      await Poll.UntilAsync(
        () => MainThread.InvokeOnMainThreadAsync(() => page.PersonPhotoForTest.Height),
        photoHeight => photoHeight > 0,
        timeoutMessage: "The photo never got arranged with a height of its own.");

      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        var host = page.PersonPhotoForTest;
        Assert.True(page.IsWideLayout, $"A {windowWidth}x{windowHeight} window should use the wide arrangement.");

        var frame = (Grid)host.Content;
        var pictures = frame.Children.OfType<Image>();
        Assert.All(pictures, picture => Assert.True(
          picture.Height <= host.Height + 0.5,
          $"The photo is {picture.Height} tall inside a box only {host.Height} tall, so WinUI paints the overhang over the header."));
      });
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        hostWindow.Width = originalWidth;
        hostWindow.Height = originalHeight;
      });
    }
  }

  // Drives the real host window, not ForceSizeAllocated: that only flips the arrangement, and it is
  // the geometry that broke -- the biography was arranged past the body's bottom edge, where MAUI
  // puts a trailing Auto row whose grid holds both a star row and a column-spanning child.
  private static async Task AssertEveryRegionIsDrawnAsync(double windowWidth, double windowHeight)
  {
    var services = new TestServices();
    var person = CreateSamplePerson() with { Biography = LongBiography() };
    var children = Enumerable.Range(2, RelativesBelowCrashThreshold).Select(Child).ToArray();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.RelativesProvider.Setup(r => r.GetChildren(It.IsAny<RelativeInfo[]>())).Returns(children);
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    var hostWindow = Application.Current!.Windows[0];
    var (originalWidth, originalHeight) = (hostWindow.Width, hostWindow.Height);
    try
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        hostWindow.Width = windowWidth;
        hostWindow.Height = windowHeight;
      });
      await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
      await Poll.UntilAsync(
        () => MainThread.InvokeOnMainThreadAsync(() => page.RelativesListForTest.Height),
        relativesHeight => relativesHeight > 0,
        timeoutMessage: "The relatives list never got arranged with a height of its own.");

      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        AssertIsDrawnInsideItsParent(page.RelativesListForTest, "relatives list");

        // Narrow, the photo scrolls inside the list header, where being arranged past the parent's
        // bottom edge is what scrolling means rather than a region going undrawn.
        if (page.IsWideLayout)
        {
          AssertIsDrawnInsideItsParent(page.PersonPhotoForTest, "person photo");
        }
      });

      // Each tab owns the whole body in turn, so the biography is only arranged once it is current.
      await MainThread.InvokeOnMainThreadAsync(() => page.InvokePageCommandAsync("TabBiography"));
      await Poll.UntilAsync(
        () => MainThread.InvokeOnMainThreadAsync(() => page.BiographyForTest.Height),
        biographyHeight => biographyHeight > 0,
        timeoutMessage: "The biography never got arranged with a height of its own.");

      await MainThread.InvokeOnMainThreadAsync(() => AssertIsDrawnInsideItsParent(page.BiographyForTest, "biography"));
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        hostWindow.Width = originalWidth;
        hostWindow.Height = originalHeight;
      });
    }
  }

  // A region arranged past its parent's bottom edge is simply never drawn -- no clipping warning,
  // no exception, just missing content.
  private static void AssertIsDrawnInsideItsParent(View view, string name)
  {
    var parent = (View)view.Parent;
    Assert.True(
      view.Height > 0,
      $"The {name} was arranged with no height at all inside a parent {parent.Height} tall.");
    Assert.True(
      view.Y + view.Height <= parent.Height + 0.5,
      $"The {name} is arranged from {view.Y} to {view.Y + view.Height} inside a parent only {parent.Height} tall, so its bottom edge is off the page.");
  }

  // Both ends matter: too tall means it sized to its content and never virtualized, and near-zero
  // means a sibling Auto row ate the space and the list renders empty.
  private static void AssertListFillsAViewport(double relativesHeight, double pageHeight, string when)
  {
    Assert.True(
      relativesHeight <= pageHeight,
      $"The relatives list is {relativesHeight} tall inside a {pageHeight} page{when}, so it sizes to its content rather than scrolling within a viewport of its own.");
    Assert.True(
      relativesHeight >= RelativeRowHeightEstimate,
      $"The relatives list is only {relativesHeight} tall inside a {pageHeight} page{when}, so it was starved of the space it needs to show even one row.");
  }

  // One row measured ~99px; a viewport worth keeping shows at least a couple.
  private const double RelativeRowHeightEstimate = 200;

  [Fact]
  public async Task PersonLinkTapped_navigates_to_the_referenced_person()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    var linkedPerson = CreateSamplePerson() with { Id = 2 };
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.Is<Person>(x => x.Id == 1), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.Is<Person>(x => x.Id == 2), It.IsAny<CancellationToken>())).ReturnsAsync(linkedPerson);
    services.Persons.Setup(p => p.TryGetPersonByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(linkedPerson);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await WaitForLoadAsync(page, services, () => page.InvokePersonLinkTappedAsync(2));

    Assert.Equal(linkedPerson.Id, page.PersonFullInfo.Id);
  }

  [Fact]
  public async Task PersonLinkTapped_with_a_dangling_id_is_inert()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    services.Persons.Setup(p => p.TryGetPersonByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Person?)null);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var loadsBefore = page.CompletedLoads;

    await page.InvokePersonLinkTappedAsync(999);

    Assert.Equal(loadsBefore, page.CompletedLoads);
    Assert.Equal(person.Id, page.PersonFullInfo.Id);
  }

  [Fact]
  public async Task AttachmentLinkTapped_with_a_dangling_id_is_inert()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    await page.InvokeAttachmentLinkTappedAsync(999);

    services.Data.Verify(table => table.TryGetDataByIdAsync(999, It.IsAny<CancellationToken>()), Times.Once());
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }

  [Fact]
  public async Task AttachmentLinkTapped_resolves_a_document_the_person_does_not_carry()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE deed.pdf\n1 TITL Another deed\n", [1, 2, 3]);
    var deed = new Data(40, content, "application/pdf", DataCategory.PersonAttachment);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(40, It.IsAny<CancellationToken>()))
      .ReturnsAsync(deed);
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);

    var attachment = await page.ResolveAttachmentAsync(40);

    Assert.Empty(page.Attachments);
    Assert.NotNull(attachment);
    Assert.Equal("Another deed", attachment.Title);
  }

  [Fact]
  public async Task AttachmentLinkTapped_with_an_id_naming_a_photo_is_inert()
  {
    var services = new TestServices();
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [1, 2, 3]);
    var mainPhoto = new Data(41, content, "image/png", DataCategory.PersonMainPhotoTagged);
    services.Data
      .Setup(table => table.TryGetDataByIdAsync(41, It.IsAny<CancellationToken>()))
      .ReturnsAsync(mainPhoto);
    var page = await CreatePageAsync(services);

    var attachment = await page.ResolveAttachmentAsync(41);

    Assert.Null(attachment);
  }

  [Fact]
  public async Task GoToFamilyTree_navigates_with_a_plain_PersonInfo()
  {
    var services = new TestServices();
    var person = CreateSamplePerson();
    services.PersonManager.Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>())).ReturnsAsync(person);
    var page = await CreatePageAsync(services);
    await WaitForLoadAsync(page, services, () => page.PersonInfo = person);
    var expectedRoute = $"{typeof(FamilyTreePage).Namespace}/{typeof(FamilyTreePage).Name}";

    await page.InvokePageCommandAsync("GoToFamilyTree");

    services.NavigationService.Verify(
      n => n.GoToAsync(
        expectedRoute,
        true,
        // Shell requires the exact runtime type PersonInfo, not the PersonFullInfo subclass --
        // see PersonPage.xaml.cs's own comment on this GoToFamilyTree branch.
        It.Is<Dictionary<string, object>>(d => d["PersonInfo"].GetType() == typeof(PersonInfo))),
      Times.Once());
  }
}
