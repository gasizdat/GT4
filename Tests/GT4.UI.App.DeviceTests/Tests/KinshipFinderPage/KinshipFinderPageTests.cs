using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers KinshipFinderPage's person-picking (through the modal SelectPersonDialog it pushes) and
/// its Find command against a mocked IKinshipFinder.
/// </summary>
public class KinshipFinderPageTests
{
  private static PersonInfo P(int id, BiologicalSex sex = BiologicalSex.Unknown, Name[]? names = null) =>
    new(id, Date.Create(null, null, null, DateStatus.Unknown), null, sex, names ?? [], null);

  private static async Task<TestableKinshipFinderPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableKinshipFinderPage>());
  }

  private static async Task PickPersonAsync(TestableKinshipFinderPage page, string commandName, PersonInfo person)
  {
    var commandTask = await MainThreadTask.StartAsync(() => page.InvokePageCommandAsync(commandName));
    var dialog = await ModalDialogHarness.WaitForModalAsync<SelectPersonDialog>(page);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.SelectedPerson = person;
      dialog.DialogCommand.Execute("SelectPersonCommand");
    });
    await commandTask;
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_starts_with_nobody_selected()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.Equal(Resources.UIStrings.FieldNotSelected, page.PersonFromName);
    Assert.Equal(Resources.UIStrings.FieldNotSelected, page.PersonToName);
    Assert.False(page.HasChain);
    Assert.False(page.ShowNotFound);
  }

  [Fact]
  public async Task Picking_both_people_finds_nothing_by_default()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    await PickPersonAsync(page, "PickPersonFrom", P(1));
    Assert.False(page.HasChain);
    Assert.False(page.ShowNotFound);

    await PickPersonAsync(page, "PickPersonTo", P(2));
    Assert.False(page.HasChain);
    Assert.True(page.ShowNotFound);
  }

  [Fact]
  public async Task Picking_the_second_person_auto_populates_the_chain_from_KinshipFinder()
  {
    var services = new TestServices();
    var personFrom = P(1);
    var personTo = P(2);
    var relative = new RelativeInfo(personTo, RelationshipType.Parent, null, Generation.Parent, Consanguinity.Zero);
    services.KinshipFinder
      .Setup(k => k.FindPathAsync(
        It.Is<Person>(p => p.Id == personFrom.Id),
        It.Is<Person>(p => p.Id == personTo.Id),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync([relative]);
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    await PickPersonAsync(page, "PickPersonFrom", personFrom);
    await PickPersonAsync(page, "PickPersonTo", personTo);

    Assert.True(page.HasChain);
    Assert.False(page.ShowNotFound);
    Assert.Single(page.Chain);
    Assert.Equal(personTo.Id, page.Chain[0].Id);
    Assert.NotNull(page.Summary);
    Assert.Equal(personTo.Id, page.Summary!.Id);
    Assert.Equal(RelationshipType.Parent, page.Summary.Type);
  }

  [Fact]
  public async Task Picking_the_second_person_shows_not_found_when_KinshipFinder_returns_no_path()
  {
    var services = new TestServices();
    var personFrom = P(1);
    var personTo = P(2);
    services.KinshipFinder
      .Setup(k => k.FindPathAsync(It.IsAny<Person>(), It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((RelativeInfo[]?)null);
    var page = await CreatePageAsync(services);

    await using var window = await WindowHost.AttachAsync(page);
    await PickPersonAsync(page, "PickPersonFrom", personFrom);
    await PickPersonAsync(page, "PickPersonTo", personTo);

    Assert.False(page.HasChain);
    Assert.True(page.ShowNotFound);
  }
}
