using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers NamesPage's dialog-free flows against a real MAUI runtime with a mocked Core.
/// PageCommand("AddName") and EditNameCommand push a modal CreateOrUpdateNameDialog via Navigation,
/// which a detached page cannot exercise; deferred until the page is hosted in a real Window.
/// </summary>
public class NamesPageTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static PersonInfo P(int id, string firstName) =>
    new(id, default(Date), null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension)], null);

  private static async Task<TestableNamesPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new TestableNamesPage(services.Provider));
  }

  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.Equal(4, page.NameTypes.Count);
    Assert.Equal(3, page.BiologicalSexes.Count);
    Assert.Equal(page.NameTypes.First(), page.CurrentNameType);
    Assert.Equal(page.BiologicalSexes.First(), page.CurrentBiologicalSex);
    Assert.NotNull(page.EditNameCommand);
    Assert.NotNull(page.DeleteNameCommand);
    Assert.NotNull(page.PageCommand);
  }

  [Fact]
  public async Task GetNamesAsync_sorts_with_the_registered_comparer()
  {
    var services = new TestServices();
    var unsorted = new[] { N(1, "Pushkin", NameType.FamilyName), N(2, "Aksakov", NameType.FamilyName), N(3, "Tolstoy", NameType.FamilyName) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(unsorted);
    var page = await CreatePageAsync(services);

    // The page's own CollectionView binding already consumed the ctor's automatic first load
    // before this test could subscribe to its completion; force a fresh one to observe.
    var names = await PageWaiter.ReloadNamesAsync(page, () => page.InvokeRequestUpdateNames());

    Assert.Equal(["Aksakov", "Pushkin", "Tolstoy"], names.Select(n => n.Value));
  }

  public static IEnumerable<object[]> DeclensionCombinations()
  {
    yield return [NameType.FamilyName, BiologicalSex.Male, NameType.FamilyName];
    yield return [NameType.FamilyName, BiologicalSex.Female, NameType.FamilyName];
    yield return [NameType.FamilyName, BiologicalSex.Unknown, NameType.FamilyName];
    yield return [NameType.FirstName, BiologicalSex.Male, NameType.FirstName | NameType.MaleDeclension];
    yield return [NameType.FirstName, BiologicalSex.Female, NameType.FirstName | NameType.FemaleDeclension];
    yield return [NameType.FirstName, BiologicalSex.Unknown, NameType.FirstName];
    yield return [NameType.Patronymic, BiologicalSex.Female, NameType.Patronymic | NameType.FemaleDeclension];
    yield return [NameType.LastName, BiologicalSex.Male, NameType.LastName | NameType.MaleDeclension];
  }

  [Theory]
  [MemberData(nameof(DeclensionCombinations))]
  public async Task Selecting_a_name_type_and_sex_requests_the_composed_declension(
    NameType nameType, BiologicalSex sex, NameType expectedComposedType)
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    var targetType = page.NameTypes.Single(t => t.Type == nameType);
    var targetSex = page.BiologicalSexes.Single(s => s.Info == sex);

    // Setting CurrentNameType to its already-current value is a no-op (guarded by record
    // equality), so only CurrentBiologicalSex's unconditional setter is relied on to trigger the
    // reload that this assertion observes.
    await PageWaiter.ReloadNamesAsync(page, () =>
    {
      page.CurrentNameType = targetType;
      page.CurrentBiologicalSex = targetSex;
    });

    services.Names.Verify(n => n.GetNamesByTypeAsync(expectedComposedType, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
  }

  [Fact]
  public async Task Setting_the_same_CurrentNameType_does_not_reload()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await PageWaiter.ReloadNamesAsync(page, () => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;

    await MainThread.InvokeOnMainThreadAsync(() => page.CurrentNameType = page.CurrentNameType);

    Assert.Equal(callsBefore, services.Names.Invocations.Count);
  }

  [Fact]
  public async Task Setting_the_same_CurrentBiologicalSex_reloads_again()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await PageWaiter.ReloadNamesAsync(page, () => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;

    await PageWaiter.ReloadNamesAsync(page, () => page.CurrentBiologicalSex = page.CurrentBiologicalSex);

    Assert.True(services.Names.Invocations.Count > callsBefore);
  }

  [Fact]
  public async Task NameFilter_matches_case_insensitively_without_reloading()
  {
    var services = new TestServices();
    var loaded = new[] { N(1, "Anna", NameType.FirstName), N(2, "Ivan", NameType.FirstName), N(3, "Hanna", NameType.FirstName) };
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(loaded);
    var page = await CreatePageAsync(services);
    await PageWaiter.ReloadNamesAsync(page, () => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;

    await MainThread.InvokeOnMainThreadAsync(() => page.NameFilter = "ANN");
    var filtered = await MainThread.InvokeOnMainThreadAsync(() => page.Names.ToArray());

    Assert.Equal(["Anna", "Hanna"], filtered.Select(n => n.Value));
    Assert.Equal(callsBefore, services.Names.Invocations.Count);

    await MainThread.InvokeOnMainThreadAsync(() => page.NameFilter = "");
    var restored = await MainThread.InvokeOnMainThreadAsync(() => page.Names.ToArray());

    Assert.Equal(3, restored.Length);
  }

  [Fact]
  public async Task CurrentName_resolves_to_the_requested_name_after_reload()
  {
    var services = new TestServices();
    var target = N(2, "Pushkin", NameType.FamilyName);
    var other = N(3, "Tolstoy", NameType.FamilyName);
    services.Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([target, other]);
    var page = await CreatePageAsync(services);

    await PageWaiter.ReloadNamesAsync(page, () => page.InvokeRequestUpdateNames(target));
    var currentName = await MainThread.InvokeOnMainThreadAsync(() => page.CurrentName);

    Assert.Equal(target.Id, currentName!.Id);

    page.InvokeRequestUpdateNames(null);
    var clearedName = await MainThread.InvokeOnMainThreadAsync(() => page.CurrentName);

    Assert.Null(clearedName);
  }

  [Fact]
  public async Task DeleteNameCommand_removes_the_name_and_requests_a_reload()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await PageWaiter.ReloadNamesAsync(page, () => page.InvokeRequestUpdateNames());
    var callsBefore = services.Names.Invocations.Count;
    var name = N(5, "Pushkin", NameType.FamilyName);

    await page.InvokeDeleteAsync(name);
    await PageWaiter.ReloadNamesAsync(page);

    services.Names.Verify(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()), Times.Once());
    services.PersonManager.Verify(p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never());
    Assert.True(services.Names.Invocations.Count > callsBefore);
  }

  [Fact]
  public async Task DeleteNameCommand_ignores_a_non_name_parameter()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await page.InvokeDeleteAsync(new object());

    services.Names.Verify(n => n.RemoveNameWithSubnamesAsync(It.IsAny<Name>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task DeleteNameCommand_reports_persons_sharing_the_root_name()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var name = N(5, "Pushkin", NameType.FamilyName);
    services.Names
      .Setup(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("FK constraint"));
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(name, false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, "Alexander")]);

    var ex = await Record.ExceptionAsync(() => page.InvokeDeleteAsync(name));

    var applicationException = Assert.IsType<ApplicationException>(ex);
    Assert.Contains("Pushkin", applicationException.Message);
    Assert.Contains("Alexander", applicationException.Message);
  }

  [Fact]
  public async Task DeleteNameCommand_reports_persons_sharing_a_subname()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var name = N(5, "Pushkin", NameType.FamilyName);
    var subname = N(6, "Pushkina", NameType.LastName | NameType.FemaleDeclension);
    services.Names
      .Setup(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("FK constraint"));
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(name, false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(name.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([subname]);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(subname, false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([P(1, "Natalia")]);

    var ex = await Record.ExceptionAsync(() => page.InvokeDeleteAsync(name));

    var applicationException = Assert.IsType<ApplicationException>(ex);
    Assert.Contains("Pushkina", applicationException.Message);
    Assert.Contains("Natalia", applicationException.Message);
  }

  [Fact]
  public async Task DeleteNameCommand_rethrows_the_original_exception_when_nothing_is_shared()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var name = N(5, "Pushkin", NameType.FamilyName);
    var originalException = new InvalidOperationException("FK constraint");
    services.Names
      .Setup(n => n.RemoveNameWithSubnamesAsync(name, It.IsAny<CancellationToken>()))
      .ThrowsAsync(originalException);
    services.PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), false, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    services.Names
      .Setup(n => n.TryGetNameWithSubnamesByIdAsync(name.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var ex = await Record.ExceptionAsync(() => page.InvokeDeleteAsync(name));

    Assert.Same(originalException, ex);
  }
}
