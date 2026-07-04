using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
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
  public Mock<IPageAlertService> PageAlertService { get; } = new();
  public Mock<INavigationService> NavigationService { get; } = new();
  public IServiceProvider Provider { get; }

  public TestServices()
  {
    Project.SetupGet(p => p.Names).Returns(Names.Object);
    Project.SetupGet(p => p.PersonManager).Returns(PersonManager.Object);
    Project.SetupGet(p => p.FamilyManager).Returns(FamilyManager.Object);
    Project.Setup(p => p.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Transaction.Object);

    CurrentProjectProvider.SetupGet(p => p.Project).Returns(Project.Object);
    CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(true);

    // GetNamesByTypeAsync returns Task<Name[]>, so Moq's own default-value provider already
    // auto-resolves an unconfigured call to Task.FromResult(Array.Empty<Name>()) (it special-cases
    // Array). Pinned explicitly anyway so this doesn't silently regress to a null-returning default
    // if the return type ever stops being a bare array -- background loads must never throw: SafeTask
    // routes failures to the page's injected IPageAlertService, and an unconfigured mock call would
    // otherwise fail invisibly.
    Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var services = new ServiceCollection();
    GT4Services.Add(services);
    services.AddSingleton(CurrentProjectProvider.Object);
    services.AddSingleton(PageAlertService.Object);
    services.AddSingleton(NavigationService.Object);
    services.AddSingleton<TestableNamesPage>();
    Provider = services.BuildServiceProvider();
  }
}
