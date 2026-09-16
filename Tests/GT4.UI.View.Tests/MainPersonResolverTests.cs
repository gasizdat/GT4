using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class MainPersonResolverTests
{
  private static readonly FileDescription Origin =
    new(new DirectoryDescription(Environment.SpecialFolder.MyDocuments, ["a"]), "sample.gt4", null);

  private static readonly ProjectInfo Project = new("Sample", "", null, Origin);

  private static PersonInfo MakePersonInfo(int id, string firstName) =>
    new(new Person(id, Date.Now, null, BiologicalSex.Female), [new Name(1, firstName, NameType.FirstName, null)], null);

  private sealed class Fixture
  {
    public Mock<IMainPersonStore> Store { get; } = new();
    public Mock<IAlertService> AlertService { get; } = new();
    public Mock<IProjectDocument> Document { get; } = new();
    public Mock<ITablePersons> Persons { get; } = new();
    public Mock<IPersonManager> PersonManager { get; } = new();

    public Fixture()
    {
      Document.SetupGet(d => d.Persons).Returns(Persons.Object);
      Document.SetupGet(d => d.PersonManager).Returns(PersonManager.Object);
    }

    public MainPersonResolver Resolver => new(Store.Object, AlertService.Object);
  }

  [Fact]
  public async Task TryResolveAsync_WhenNothingStored_ReturnsNullWithoutTouchingTheDocument()
  {
    var fixture = new Fixture();
    fixture.Store.Setup(s => s.Get(Project)).Returns((MainPersonInfo?)null);

    var result = await fixture.Resolver.TryResolveAsync(Project, fixture.Document.Object, CancellationToken.None);

    result.Should().BeNull();
    fixture.Persons.Verify(p => p.TryGetPersonByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task TryResolveAsync_WhenStoredPersonExists_ReturnsTheEnrichedPersonInfo()
  {
    var fixture = new Fixture();
    var person = new Person(7, Date.Now, null, BiologicalSex.Female);
    var personInfo = MakePersonInfo(7, "Ada");
    fixture.Store.Setup(s => s.Get(Project)).Returns(new MainPersonInfo(7, "Ada"));
    fixture.Persons.Setup(p => p.TryGetPersonByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(person);
    fixture.PersonManager
      .Setup(m => m.GetPersonInfosAsync(new[] { person }, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([personInfo]);

    var result = await fixture.Resolver.TryResolveAsync(Project, fixture.Document.Object, CancellationToken.None);

    result.Should().Be(personInfo);
    fixture.Store.Verify(s => s.Clear(It.IsAny<ProjectInfo>()), Times.Never);
  }

  [Fact]
  public async Task TryResolveAsync_WhenStoredPersonIsMissing_ClearsAndWarnsOnceAndReturnsNull()
  {
    var fixture = new Fixture();
    fixture.Store.Setup(s => s.Get(Project)).Returns(new MainPersonInfo(7, "Ada"));
    fixture.Persons.Setup(p => p.TryGetPersonByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Person?)null);

    var result = await fixture.Resolver.TryResolveAsync(Project, fixture.Document.Object, CancellationToken.None);

    result.Should().BeNull();
    fixture.Store.Verify(s => s.Clear(Project), Times.Once);
    fixture.AlertService.Verify(a => a.ShowWarningAsync(It.IsAny<string>()), Times.Once);
  }

  [Fact]
  public async Task TryResolveAsync_WhenTheStoredPersonWasRenamed_RefreshesTheStoredDisplayName()
  {
    var fixture = new Fixture();
    var person = new Person(7, Date.Now, null, BiologicalSex.Female);
    var renamed = MakePersonInfo(7, "Augusta");
    fixture.Store.Setup(s => s.Get(Project)).Returns(new MainPersonInfo(7, "Ada"));
    fixture.Persons.Setup(p => p.TryGetPersonByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(person);
    fixture.PersonManager
      .Setup(m => m.GetPersonInfosAsync(new[] { person }, true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([renamed]);

    var result = await fixture.Resolver.TryResolveAsync(Project, fixture.Document.Object, CancellationToken.None);

    result.Should().Be(renamed);
    fixture.Store.Verify(s => s.Set(Project, 7, "Augusta"), Times.Once);
  }
}
