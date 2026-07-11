using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class DataCategoryExtensionsTests
{
  [Theory]
  [InlineData(DataCategory.PersonMainPhotoTagged, true)]
  [InlineData(DataCategory.PersonPhotoTagged, true)]
  [InlineData(DataCategory.PersonMainPhoto, false)]
  [InlineData(DataCategory.PersonPhoto, false)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsTaggedPhoto_IdentifiesOnlyTheTwoTaggedPhotoCategories(DataCategory category, bool expected) =>
    category.IsTaggedPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, DataCategory.PersonMainPhoto)]
  [InlineData(DataCategory.PersonPhoto, DataCategory.PersonMainPhoto)]
  [InlineData(DataCategory.PersonMainPhotoTagged, DataCategory.PersonMainPhotoTagged)]
  [InlineData(DataCategory.PersonPhotoTagged, DataCategory.PersonMainPhotoTagged)]
  public void AsMainPhoto_PreservesTaggedness(DataCategory category, DataCategory expected) =>
    category.AsMainPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, DataCategory.PersonPhoto)]
  [InlineData(DataCategory.PersonPhoto, DataCategory.PersonPhoto)]
  [InlineData(DataCategory.PersonMainPhotoTagged, DataCategory.PersonPhotoTagged)]
  [InlineData(DataCategory.PersonPhotoTagged, DataCategory.PersonPhotoTagged)]
  public void AsAdditionalPhoto_PreservesTaggedness(DataCategory category, DataCategory expected) =>
    category.AsAdditionalPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, DataCategory.PersonMainPhoto)]
  [InlineData(DataCategory.PersonPhoto, DataCategory.PersonPhoto)]
  [InlineData(DataCategory.PersonMainPhotoTagged, DataCategory.PersonMainPhoto)]
  [InlineData(DataCategory.PersonPhotoTagged, DataCategory.PersonPhoto)]
  public void AsPlainPhoto_AlwaysDowngradesToPlain(DataCategory category, DataCategory expected) =>
    category.AsPlainPhoto().Should().Be(expected);

  [Fact]
  public void AsPlainPhoto_LeavesNonPhotoCategoriesUnchanged()
  {
    DataCategory.PersonBio.AsPlainPhoto().Should().Be(DataCategory.PersonBio);
    DataCategory.PersonGedcomTags.AsPlainPhoto().Should().Be(DataCategory.PersonGedcomTags);
  }

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, DataCategory.PersonMainPhotoTagged)]
  [InlineData(DataCategory.PersonMainPhotoTagged, DataCategory.PersonMainPhotoTagged)]
  [InlineData(DataCategory.PersonPhoto, DataCategory.PersonPhotoTagged)]
  [InlineData(DataCategory.PersonPhotoTagged, DataCategory.PersonPhotoTagged)]
  public void AsTaggedPhoto_AlwaysUpgradesToTagged(DataCategory category, DataCategory expected) =>
    category.AsTaggedPhoto().Should().Be(expected);

  [Fact]
  public void AsTaggedPhoto_LeavesNonPhotoCategoriesUnchanged()
  {
    DataCategory.PersonBio.AsTaggedPhoto().Should().Be(DataCategory.PersonBio);
    DataCategory.PersonGedcomTags.AsTaggedPhoto().Should().Be(DataCategory.PersonGedcomTags);
  }
}
