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
  [InlineData(DataCategory.FamilyMainPhoto, false)]
  [InlineData(DataCategory.FamilyPhoto, false)]
  public void IsTaggedPhoto_IdentifiesOnlyTheTwoTaggedPhotoCategories(DataCategory category, bool expected) =>
    category.IsTaggedPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, true)]
  [InlineData(DataCategory.PersonMainPhotoTagged, true)]
  [InlineData(DataCategory.FamilyMainPhoto, true)]
  [InlineData(DataCategory.PersonPhoto, false)]
  [InlineData(DataCategory.PersonPhotoTagged, false)]
  [InlineData(DataCategory.FamilyPhoto, false)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsMainPhoto_IdentifiesPlainOrTaggedMainPhoto(DataCategory category, bool expected) =>
    category.IsMainPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonPhoto, true)]
  [InlineData(DataCategory.PersonPhotoTagged, true)]
  [InlineData(DataCategory.FamilyPhoto, true)]
  [InlineData(DataCategory.PersonMainPhoto, false)]
  [InlineData(DataCategory.PersonMainPhotoTagged, false)]
  [InlineData(DataCategory.FamilyMainPhoto, false)]
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
  [InlineData(DataCategory.FamilyMainPhoto, DataCategory.FamilyMainPhoto)]
  [InlineData(DataCategory.FamilyPhoto, DataCategory.FamilyMainPhoto)]
  public void AsMainPhoto_HasNoTaggedVariantForFamilyCategories(DataCategory category, DataCategory expected) =>
    category.AsMainPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonBio)]
  [InlineData(DataCategory.PersonGedcomTags)]
  public void AsMainPhoto_ThrowsForNonPhotoCategories(DataCategory category) =>
    FluentActions.Invoking(() => category.AsMainPhoto()).Should().Throw<ArgumentOutOfRangeException>();

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, DataCategory.PersonPhoto)]
  [InlineData(DataCategory.PersonPhoto, DataCategory.PersonPhoto)]
  [InlineData(DataCategory.PersonMainPhotoTagged, DataCategory.PersonPhotoTagged)]
  [InlineData(DataCategory.PersonPhotoTagged, DataCategory.PersonPhotoTagged)]
  public void AsAdditionalPhoto_PreservesTaggedness(DataCategory category, DataCategory expected) =>
    category.AsAdditionalPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.FamilyMainPhoto, DataCategory.FamilyPhoto)]
  [InlineData(DataCategory.FamilyPhoto, DataCategory.FamilyPhoto)]
  public void AsAdditionalPhoto_HasNoTaggedVariantForFamilyCategories(DataCategory category, DataCategory expected) =>
    category.AsAdditionalPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonBio)]
  [InlineData(DataCategory.PersonGedcomTags)]
  public void AsAdditionalPhoto_ThrowsForNonPhotoCategories(DataCategory category) =>
    FluentActions.Invoking(() => category.AsAdditionalPhoto()).Should().Throw<ArgumentOutOfRangeException>();

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
  [InlineData(DataCategory.FamilyMainPhoto)]
  [InlineData(DataCategory.FamilyPhoto)]
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
  [InlineData(DataCategory.FamilyMainPhoto)]
  [InlineData(DataCategory.FamilyPhoto)]
  public void AsTaggedPhoto_ThrowsForNonPhotoCategories(DataCategory category) =>
    FluentActions.Invoking(() => category.AsTaggedPhoto()).Should().Throw<ArgumentOutOfRangeException>();

  [Theory]
  [InlineData(DataCategory.PersonMainPhoto, true)]
  [InlineData(DataCategory.PersonMainPhotoTagged, true)]
  [InlineData(DataCategory.PersonPhoto, true)]
  [InlineData(DataCategory.PersonPhotoTagged, true)]
  [InlineData(DataCategory.FamilyMainPhoto, true)]
  [InlineData(DataCategory.FamilyPhoto, true)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsPhoto_IdentifiesAnyPhotoCategory(DataCategory category, bool expected) =>
    category.IsPhoto().Should().Be(expected);

  [Theory]
  [InlineData(DataCategory.PersonAttachment, true)]
  [InlineData(DataCategory.FamilyAttachment, true)]
  [InlineData(DataCategory.PersonMainPhoto, false)]
  [InlineData(DataCategory.PersonBio, false)]
  [InlineData(DataCategory.PersonGedcomTags, false)]
  public void IsAttachment_IdentifiesPersonAndFamilyAttachment(DataCategory category, bool expected) =>
    category.IsAttachment().Should().Be(expected);
}
