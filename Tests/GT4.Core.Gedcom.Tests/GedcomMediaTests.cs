using FluentAssertions;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomMediaTests
{
  private static readonly byte[] PngBytes = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

  [Fact]
  public void ResolveForm_OctetStreamOverImageBytes_TakesTheFormFromTheBytes() =>
    GedcomMedia.ResolveForm("application/octet-stream", PngBytes).Should().Be("png");

  // Nothing else names the format, so the placeholder still has to yield a form -- dropping it would leave
  // the sidecar extensionless and the OBJE without a FORM to reimport the MIME from.
  [Fact]
  public void ResolveForm_OctetStreamOverUnrecognizedBytes_KeepsThePlaceholderForm() =>
    GedcomMedia.ResolveForm("application/octet-stream", [1, 2, 3]).Should().Be("octet-stream");

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
