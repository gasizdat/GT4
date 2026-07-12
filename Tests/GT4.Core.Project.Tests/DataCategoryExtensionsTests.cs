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
  [InlineData(DataCategory.PersonMainPhoto, true)]
  [InlineData(DataCategory.PersonMainPhotoTagged, true)]
  [InlineData(DataCategory.PersonPhoto, false)]
  [InlineData(DataCategory.PersonPhotoTagged, false)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsMainPhoto_IdentifiesPlainOrTaggedMainPhoto(DataCategory category, bool expected) =>
    category.IsMainPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonPhoto, true)]
  [InlineData(DataCategory.PersonPhotoTagged, true)]
  [InlineData(DataCategory.PersonMainPhoto, false)]
  [InlineData(DataCategory.PersonMainPhotoTagged, false)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsAdditionalPhoto_IdentifiesPlainOrTaggedAdditionalPhoto(DataCategory category, bool expected) =>
    category.IsAdditionalPhoto().Should().Be(expected);

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

  [Theory]
  [InlineData(DataCategory.PersonBio)]
  [InlineData(DataCategory.PersonGedcomTags)]
  public void AsPlainPhoto_ThrowsForNonPhotoCategories(DataCategory category) =>
    FluentActions.Invoking(() => category.AsPlainPhoto()).Should().Throw<ArgumentOutOfRangeException>();

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, DataCategory.PersonMainPhotoTagged)]
  [InlineData(DataCategory.PersonMainPhotoTagged, DataCategory.PersonMainPhotoTagged)]
  [InlineData(DataCategory.PersonPhoto, DataCategory.PersonPhotoTagged)]
  [InlineData(DataCategory.PersonPhotoTagged, DataCategory.PersonPhotoTagged)]
  public void AsTaggedPhoto_AlwaysUpgradesToTagged(DataCategory category, DataCategory expected) =>
    category.AsTaggedPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonBio)]
  [InlineData(DataCategory.PersonGedcomTags)]
  public void AsTaggedPhoto_ThrowsForNonPhotoCategories(DataCategory category) =>
    FluentActions.Invoking(() => category.AsTaggedPhoto()).Should().Throw<ArgumentOutOfRangeException>();

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, true)]
  [InlineData(DataCategory.PersonMainPhotoTagged, true)]
  [InlineData(DataCategory.PersonPhoto, true)]
  [InlineData(DataCategory.PersonPhotoTagged, true)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsPhoto_IdentifiesAnyOfTheFourPhotoCategories(DataCategory category, bool expected) =>
    category.IsPhoto().Should().Be(expected);
}
