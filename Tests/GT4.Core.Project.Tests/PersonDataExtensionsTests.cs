using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using Moq;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class PersonDataExtensionsTests
{
  private CancellationToken Token => TestContext.Current.CancellationToken;

  private static Person MakePerson(int id) => new(id, Date.Now, null, BiologicalSex.Male);

  private static Data MakePhoto(int id, DataCategory category) => new(id, [], null, category);

  [Fact]
  public async Task GetMergedPhotoSetAsync_PersonInBothSets_ConcatenatesPlainAndTagged()
  {
    var persons = new[] { MakePerson(1) };
    var plain = new[] { MakePhoto(10, DataCategory.PersonPhoto) };
    var tagged = new[] { MakePhoto(11, DataCategory.PersonPhotoTagged) };
    var personData = new Mock<ITablePersonData>(MockBehavior.Strict);
    personData
      .Setup(p => p.GetPersonDataSetAsync(persons, DataCategory.PersonPhoto, Token))
      .ReturnsAsync(new Dictionary<int, Data[]> { [1] = plain });
    personData
      .Setup(p => p.GetPersonDataSetAsync(persons, DataCategory.PersonPhotoTagged, Token))
      .ReturnsAsync(new Dictionary<int, Data[]> { [1] = tagged });

    var result = await personData.Object.GetMergedPhotoSetAsync(persons, DataCategory.PersonPhoto, Token);

    result[1].Should().BeEquivalentTo(plain.Concat(tagged));
  }

  [Fact]
  public async Task GetMergedPhotoSetAsync_PersonOnlyInTaggedSet_UsesTaggedPhotosDirectly()
  {
    var persons = new[] { MakePerson(1) };
    var tagged = new[] { MakePhoto(11, DataCategory.PersonPhotoTagged) };
    var personData = new Mock<ITablePersonData>(MockBehavior.Strict);
    personData
      .Setup(p => p.GetPersonDataSetAsync(persons, DataCategory.PersonPhoto, Token))
      .ReturnsAsync([]);
    personData
      .Setup(p => p.GetPersonDataSetAsync(persons, DataCategory.PersonPhotoTagged, Token))
      .ReturnsAsync(new Dictionary<int, Data[]> { [1] = tagged });

    var result = await personData.Object.GetMergedPhotoSetAsync(persons, DataCategory.PersonPhoto, Token);

    result[1].Should().BeEquivalentTo(tagged);
  }

  [Fact]
  public async Task GetMergedPhotoSetAsync_NoPhotosForPerson_OmitsThemFromResult()
  {
    var persons = new[] { MakePerson(1) };
    var personData = new Mock<ITablePersonData>(MockBehavior.Strict);
    personData
      .Setup(p => p.GetPersonDataSetAsync(persons, DataCategory.PersonMainPhoto, Token))
      .ReturnsAsync([]);
    personData
      .Setup(p => p.GetPersonDataSetAsync(persons, DataCategory.PersonMainPhotoTagged, Token))
      .ReturnsAsync([]);

    var result = await personData.Object.GetMergedPhotoSetAsync(persons, DataCategory.PersonMainPhoto, Token);

    result.Should().BeEmpty();
  }
}
