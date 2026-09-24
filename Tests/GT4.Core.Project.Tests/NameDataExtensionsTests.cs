using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using Moq;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class NameDataExtensionsTests
{
  private CancellationToken Token => TestContext.Current.CancellationToken;

  private static Name MakeName(int id) => new(id, "Bourbon", NameType.FamilyName, null);

  private static Data MakePhoto(int id, DataCategory category) => new(id, [], null, category);

  [Fact]
  public async Task GetMergedPhotoSetAsync_NameInBothSets_ConcatenatesPlainAndTagged()
  {
    var names = new[] { MakeName(1) };
    var plain = new[] { MakePhoto(10, DataCategory.FamilyPhoto) };
    var tagged = new[] { MakePhoto(11, DataCategory.FamilyPhotoTagged) };
    var nameData = new Mock<ITableNameData>(MockBehavior.Strict);
    nameData
      .Setup(n => n.GetNameDataSetAsync(names, DataCategory.FamilyPhoto, Token))
      .ReturnsAsync(new Dictionary<int, Data[]> { [1] = plain });
    nameData
      .Setup(n => n.GetNameDataSetAsync(names, DataCategory.FamilyPhotoTagged, Token))
      .ReturnsAsync(new Dictionary<int, Data[]> { [1] = tagged });

    var result = await nameData.Object.GetMergedPhotoSetAsync(names, DataCategory.FamilyPhoto, Token);

    result[1].Should().BeEquivalentTo(plain.Concat(tagged));
  }

  [Fact]
  public async Task GetMergedPhotoSetAsync_NameOnlyInTaggedSet_UsesTaggedPhotosDirectly()
  {
    var names = new[] { MakeName(1) };
    var tagged = new[] { MakePhoto(11, DataCategory.FamilyPhotoTagged) };
    var nameData = new Mock<ITableNameData>(MockBehavior.Strict);
    nameData
      .Setup(n => n.GetNameDataSetAsync(names, DataCategory.FamilyPhoto, Token))
      .ReturnsAsync([]);
    nameData
      .Setup(n => n.GetNameDataSetAsync(names, DataCategory.FamilyPhotoTagged, Token))
      .ReturnsAsync(new Dictionary<int, Data[]> { [1] = tagged });

    var result = await nameData.Object.GetMergedPhotoSetAsync(names, DataCategory.FamilyPhoto, Token);

    result[1].Should().BeEquivalentTo(tagged);
  }

  [Fact]
  public async Task GetMergedPhotoSetAsync_NoPhotosForName_OmitsThemFromResult()
  {
    var names = new[] { MakeName(1) };
    var nameData = new Mock<ITableNameData>(MockBehavior.Strict);
    nameData
      .Setup(n => n.GetNameDataSetAsync(names, DataCategory.FamilyMainPhoto, Token))
      .ReturnsAsync([]);
    nameData
      .Setup(n => n.GetNameDataSetAsync(names, DataCategory.FamilyMainPhotoTagged, Token))
      .ReturnsAsync([]);

    var result = await nameData.Object.GetMergedPhotoSetAsync(names, DataCategory.FamilyMainPhoto, Token);

    result.Should().BeEmpty();
  }
}
