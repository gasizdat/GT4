using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;

namespace GT4.UI.DeviceTests;

/// <summary>
/// A fresh DI container per test, built from the app's real composition root (GT4Services.Add)
/// with the project-document graph replaced by mocks. Everything else (formatters, comparers,
/// the real ICancellationTokenProvider) comes from the app unmodified.
/// </summary>
internal sealed class TestServices
{
  public Mock<ICurrentProjectProvider> CurrentProjectProvider { get; } = new();
  public Mock<IProjectDocument> Project { get; } = new();
  public Mock<ITableNames> Names { get; } = new();
  public Mock<IPersonManager> PersonManager { get; } = new();
  public Mock<IFamilyManager> FamilyManager { get; } = new();
  public Mock<IProjectTransaction> Transaction { get; } = new();
  public Mock<ITableMetadata> Metadata { get; } = new();
  public Mock<IProjectList> ProjectList { get; } = new();
  public Mock<IRelativesProvider> RelativesProvider { get; } = new();
  public Mock<ITablePersons> Persons { get; } = new();
  public Mock<IAlertService> AlertService { get; } = new();
  public Mock<INavigationService> NavigationService { get; } = new();
  public IServiceProvider Provider { get; }

  public static readonly ProjectInfo SampleProjectInfo = new(
    Name: "Sample Project",
    Description: "",
    Revision: "",
    Origin: new FileDescription(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, []), "sample.gt4", null));

  public TestServices()
  {
    Project.SetupGet(p => p.Names).Returns(Names.Object);
    Project.SetupGet(p => p.PersonManager).Returns(PersonManager.Object);
    Project.SetupGet(p => p.FamilyManager).Returns(FamilyManager.Object);
    Project.SetupGet(p => p.Metadata).Returns(Metadata.Object);
    Project.SetupGet(p => p.RelativesProvider).Returns(RelativesProvider.Object);
    Project.SetupGet(p => p.Persons).Returns(Persons.Object);
    Project.Setup(p => p.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Transaction.Object);

    CurrentProjectProvider.SetupGet(p => p.Project).Returns(Project.Object);
    CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(true);
    CurrentProjectProvider.SetupGet(p => p.Info).Returns(SampleProjectInfo);

    // Same reasoning as GetNamesByTypeAsync below: ProjectPage.Families loads in the background
    // (well, blocks on it), so an unconfigured call must not fail invisibly through IAlertService.
    FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

    // GetNamesByTypeAsync returns Task<Name[]>, so Moq's own default-value provider already
    // auto-resolves an unconfigured call to Task.FromResult(Array.Empty<Name>()) (it special-cases
    // Array). Pinned explicitly anyway so this doesn't silently regress to a null-returning default
    // if the return type ever stops being a bare array -- background loads must never throw: SafeTask
    // routes failures to the page's injected IAlertService, and an unconfigured mock call would
    // otherwise fail invisibly.
    Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    // Same reasoning as GetNamesByTypeAsync above: FamilyPage.Persons loads in the background
    // (well, blocks on it), so an unconfigured call must not fail invisibly through IAlertService.
    PersonManager
      .Setup(p => p.GetPersonInfosByNameAsync(It.IsAny<Name>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    // PersonPage.GetPersonDataAsync's whole pipeline needs every one of these non-null (a null Moq
    // default for a custom record/array causes an NRE that gets caught and reported through
    // IAlertService -- which reads as a mysterious timeout in the counter-based load wait, not an
    // obvious error, unless every collaborator has a safe default up front).
    PersonManager
      .Setup(p => p.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(PersonFullInfo.Empty);
    RelativesProvider
      .Setup(r => r.GetParentsAsync(It.IsAny<RelativeInfo[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Parents([], [], []));
    RelativesProvider
      .Setup(r => r.GetStepChildrenAsync(It.IsAny<RelativeInfo[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    RelativesProvider
      .Setup(r => r.GetSiblings(It.IsAny<Person>(), It.IsAny<Parents>()))
      .Returns(new Siblings([], [], [], [], []));
    RelativesProvider.Setup(r => r.GetChildren(It.IsAny<RelativeInfo[]>())).Returns([]);
    RelativesProvider.Setup(r => r.GetAdoptiveChildren(It.IsAny<RelativeInfo[]>())).Returns([]);

    var services = new ServiceCollection();
    GT4Services.Add(services);
    services.AddSingleton(CurrentProjectProvider.Object);
    services.AddSingleton(AlertService.Object);
    services.AddSingleton(NavigationService.Object);
    services.AddSingleton(ProjectList.Object);
    services.AddSingleton<TestableNamesPage>();
    services.AddSingleton<TestableFamilyPage>();
    services.AddSingleton<TestableProjectPage>();
    services.AddSingleton<TestablePersonPage>();
    Provider = services.BuildServiceProvider();
  }
}
