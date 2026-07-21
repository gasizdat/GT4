using FluentAssertions;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomMediaTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void ToForm_NullOrEmptyMimeType_ReturnsNull(string? mimeType) =>
    GedcomMedia.ToForm(mimeType).Should().BeNull();

  [Theory]
  [InlineData("image/jpeg", "jpeg")]
  [InlineData("image/bmp", "bmp")]
  public void ToForm_ImageMimeType_StripsImagePrefix(string mimeType, string expected) =>
    GedcomMedia.ToForm(mimeType).Should().Be(expected);

  [Fact]
  public void ToForm_ApplicationMimeType_StripsApplicationPrefix() =>
    GedcomMedia.ToForm("application/pdf").Should().Be("pdf");

  [Fact]
  public void ToForm_UnrecognizedMimeType_PassesThroughVerbatim() =>
    GedcomMedia.ToForm("text/plain").Should().Be("text/plain");

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void ToMimeType_NullOrEmptyForm_ReturnsNull(string? form) =>
    GedcomMedia.ToMimeType(form).Should().BeNull();

  [Theory]
  [InlineData("jpeg", "image/jpeg")]
  [InlineData("bmp", "image/bmp")]
  public void ToMimeType_ImageToken_AddsImagePrefix(string form, string expected) =>
    GedcomMedia.ToMimeType(form).Should().Be(expected);

  [Fact]
  public void ToMimeType_NonImageToken_AddsApplicationPrefix() =>
    GedcomMedia.ToMimeType("pdf").Should().Be("application/pdf");

  [Fact]
  public void ToMimeType_FormAlreadyCarryingSlash_PassesThroughVerbatim() =>
    GedcomMedia.ToMimeType("application/pdf").Should().Be("application/pdf");
}
