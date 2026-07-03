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
  public IServiceProvider Provider { get; }

  public TestServices()
  {
    Project.SetupGet(p => p.Names).Returns(Names.Object);
    Project.SetupGet(p => p.PersonManager).Returns(PersonManager.Object);
    Project.SetupGet(p => p.FamilyManager).Returns(FamilyManager.Object);

    CurrentProjectProvider.SetupGet(p => p.Project).Returns(Project.Object);
    CurrentProjectProvider.SetupGet(p => p.HasCurrentProject).Returns(true);

    // Background loads must never throw: SafeTask routes failures to PageAlert.ShowErrorAsync,
    // which needs Shell.Current (null in this host) and would otherwise fail invisibly.
    Names
      .Setup(n => n.GetNamesByTypeAsync(It.IsAny<NameType>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var services = new ServiceCollection();
    GT4Services.Add(services);
    services.AddSingleton(CurrentProjectProvider.Object);
    Provider = services.BuildServiceProvider();
  }
}
