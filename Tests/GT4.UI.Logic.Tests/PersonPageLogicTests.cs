using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Logic;
using GT4.UI.Utils.Converters;
using Moq;
using Xunit;

namespace GT4.UI.Logic.Tests;

public sealed class PersonPageLogicTests
{
  private readonly Mock<ICurrentProjectProvider> _currentProjectProvider = new();
  private readonly Mock<ICancellationTokenProvider> _cancellationTokenProvider = new();
  private readonly Mock<IProjectDocument> _project = new();
  private readonly Mock<IPersonManager> _personManager = new();
  private readonly Mock<IRelativesProvider> _relativesProvider = new();
  private readonly Mock<ITablePersons> _persons = new();
  private readonly Mock<IDataConverter> _textConverter = new();
  private readonly Mock<IDataConverter> _gedcomConverter = new();

  private static readonly GridLayout StackedImage = new(Column: 0, ColumnSpan: 2, Row: 0, RowSpan: 1);
  private static readonly GridLayout StackedRelatives = new(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1);
  private static readonly GridLayout StackedBiography = new(Column: 0, ColumnSpan: 2, Row: 2, RowSpan: 1);

  public PersonPageLogicTests()
  {
    _cancellationTokenProvider.Setup(p => p.CreateDbCancellationToken())
                              .Returns(() => new CancellationTokenHost(TimeSpan.FromSeconds(5)));
    _currentProjectProvider.SetupGet(p => p.Project).Returns(_project.Object);
    _project.SetupGet(d => d.PersonManager).Returns(_personManager.Object);
    _project.SetupGet(d => d.RelativesProvider).Returns(_relativesProvider.Object);
    _project.SetupGet(d => d.Persons).Returns(_persons.Object);
  }

  private PersonPageLogic CreateLogic() =>
    new(_currentProjectProvider.Object, _cancellationTokenProvider.Object, _textConverter.Object, _gedcomConverter.Object);

  private static RelativeInfo Rel(int id, BiologicalSex sex, RelationshipType type) =>
    new(id, Date.Now, null, sex, [], null, type, null, default, default);

  private static PersonFullInfo FullInfo(int id, params RelativeInfo[] relativeInfos) =>
    new(Id: id, BirthDate: Date.Now, DeathDate: null, BiologicalSex: BiologicalSex.Male, Names: [],
        MainPhoto: null, AdditionalPhotos: [], RelativeInfos: relativeInfos, Biography: null, GedcomData: null);

  [Fact]
  public async Task GetPersonDataAsync_assembles_roots_in_display_order()
  {
    var spouseFemale = Rel(1, BiologicalSex.Female, RelationshipType.Spouse);
    var spouseMale = Rel(2, BiologicalSex.Male, RelationshipType.Spouse);
    var nativeParent = new RelativeFullInfo(Rel(3, BiologicalSex.Male, RelationshipType.Parent), []);
    var sibling = Rel(4, BiologicalSex.Female, RelationshipType.Sibling);
    var child = Rel(7, BiologicalSex.Male, RelationshipType.Child);
    var adoptiveChild = Rel(8, BiologicalSex.Female, RelationshipType.AdoptiveChild);
    var stepChild = Rel(9, BiologicalSex.Male, RelationshipType.StepChild);

    // Spouses arrive female-first to prove the within-group sort by biological sex (Male < Female).
    var person = new Person(10, Date.Now, null, BiologicalSex.Male);
    var fullInfo = FullInfo(10, spouseFemale, spouseMale);
    var parents = new Parents([nativeParent], [], []);
    var siblings = new Siblings([sibling], [], [], [], []);

    _personManager.Setup(m => m.GetPersonFullInfoAsync(person, It.IsAny<CancellationToken>())).ReturnsAsync(fullInfo);
    _relativesProvider.Setup(r => r.GetParentsAsync(fullInfo.RelativeInfos, It.IsAny<CancellationToken>())).ReturnsAsync(parents);
    _relativesProvider.Setup(r => r.GetStepChildrenAsync(fullInfo.RelativeInfos, It.IsAny<CancellationToken>())).ReturnsAsync([stepChild]);
    _relativesProvider.Setup(r => r.GetSiblings(It.IsAny<Person>(), parents)).Returns(siblings);
    _relativesProvider.Setup(r => r.GetChildren(fullInfo.RelativeInfos)).Returns([child]);
    _relativesProvider.Setup(r => r.GetAdoptiveChildren(fullInfo.RelativeInfos)).Returns([adoptiveChild]);

    var data = await CreateLogic().GetPersonDataAsync(person);

    data.PersonFullInfo.Should().BeSameAs(fullInfo);
    // spouses (Male,Female) → parents.Native → siblings.Native → children → adoptiveChildren → stepChildren
    data.Roots.Select(r => r.Id).Should().Equal(2, 1, 3, 4, 7, 8, 9);
  }

  [Fact]
  public async Task RemovePersonAsync_removes_the_person()
  {
    var person = new Person(10, Date.Now, null, BiologicalSex.Male);

    await CreateLogic().RemovePersonAsync(person);

    _persons.Verify(p => p.RemovePersonAsync(person, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task UpdatePersonAsync_updates_the_person()
  {
    var fullInfo = FullInfo(10);

    await CreateLogic().UpdatePersonAsync(fullInfo);

    _personManager.Verify(m => m.UpdatePersonAsync(fullInfo, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Theory]
  // bio, gedcom → combined: bio leads, GEDCOM residual follows, a blank part drops out (blank on both → empty).
  [InlineData("bio", "gedcom", "bio\n\ngedcom")]
  [InlineData("bio", null, "bio")]
  [InlineData("bio", "  ", "bio")]
  [InlineData(null, "gedcom", "gedcom")]
  [InlineData("  ", "gedcom", "gedcom")]
  [InlineData(null, null, "")]
  [InlineData(null, "  ", "")]
  public async Task CombineBiographyAsync_merges_bio_and_gedcom(string? bio, string? gedcom, string expected)
  {
    var fullInfo = FullInfo(10);
    _textConverter.Setup(c => c.ToObjectAsync(fullInfo.Biography, It.IsAny<CancellationToken>())).ReturnsAsync(bio);
    _gedcomConverter.Setup(c => c.ToObjectAsync(fullInfo.GedcomData, It.IsAny<CancellationToken>())).ReturnsAsync(gedcom);

    var combined = await CreateLogic().CombineBiographyAsync(fullInfo);

    combined.Should().Be(expected);
  }

  [Fact]
  public void ComputeLayout_stacks_blocks_when_portrait()
  {
    var layout = PersonPageLogic.ComputeLayout(width: 500, height: 900, density: 2);

    layout.Image.Should().Be(StackedImage);
    layout.Relatives.Should().Be(StackedRelatives);
    layout.Biography.Should().Be(StackedBiography);
  }

  [Fact]
  public void ComputeLayout_stacks_blocks_when_landscape_but_too_narrow()
  {
    // Landscape (width > height) yet only 800 physical px wide, below the 900 threshold → still stacked.
    var layout = PersonPageLogic.ComputeLayout(width: 800, height: 500, density: 1);

    layout.Image.Should().Be(StackedImage);
    layout.Relatives.Should().Be(StackedRelatives);
    layout.Biography.Should().Be(StackedBiography);
  }

  [Fact]
  public void ComputeLayout_places_image_and_relatives_side_by_side_when_wide_landscape()
  {
    var layout = PersonPageLogic.ComputeLayout(width: 900, height: 500, density: 1);

    layout.Image.Should().Be(new GridLayout(Column: 0, ColumnSpan: 1, Row: 0, RowSpan: 1));
    layout.Relatives.Should().Be(new GridLayout(Column: 1, ColumnSpan: 1, Row: 0, RowSpan: 1));
    layout.Biography.Should().Be(new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1));
  }
}
