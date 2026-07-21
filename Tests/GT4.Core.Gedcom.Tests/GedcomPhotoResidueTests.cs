using FluentAssertions;
using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomPhotoResidueTests
{
  private CancellationToken _Token => TestContext.Current.CancellationToken;

  private static GedcomNode Residual(params (string Tag, string Value)[] children)
  {
    var node = new GedcomNode { Tag = "OBJE" };
    foreach (var (tag, value) in children)
    {
      node.Add(new GedcomNode { Tag = tag, Value = value });
    }
    return node;
  }

  [Fact]
  public async Task EncodeDecode_RoundTripsImageBytesAndResidualTags()
  {
    byte[] image = [1, 2, 3, 4, 5, 250, 0];
    var residual = Residual(("TITL", "Louis XIII par Rubens"), ("NOTE", "scanned 2019"));

    var content = GedcomPhotoResidue.Encode(image, residual);
    var (imageBytes, decodedResidual) = await GedcomPhotoResidue.DecodeAsync(content, _Token);

    imageBytes.Should().Equal(image);
    decodedResidual.Tag.Should().Be("OBJE");
    decodedResidual.ChildValue("TITL").Should().Be("Louis XIII par Rubens");
    decodedResidual.ChildValue("NOTE").Should().Be("scanned 2019");
  }

  [Fact]
  public void ExtractImageBytes_MatchesTheImageHalfWithoutParsing()
  {
    byte[] image = [10, 20, 30, 40];
    var content = GedcomPhotoResidue.Encode(image, Residual(("TITL", "A caption")));

    GedcomPhotoResidue.ExtractImageBytes(content).Should().Equal(image);
  }

  [Fact]
  public async Task ExtractTitleAsync_ReturnsTheTitlValue()
  {
    byte[] image = [1];
    var content = GedcomPhotoResidue.Encode(image, Residual(("TITL", "A caption")));
    var photo = new Data(1, content, "image/png", DataCategory.PersonMainPhotoTagged);

    var title = await GedcomPhotoResidue.ExtractTitleAsync(photo, _Token);

    title.Should().Be("A caption");
  }

  [Fact]
  public async Task ExtractTitleAsync_ReturnsNullForAPlainCategoryPhoto_WithoutAttemptingToParse()
  {
    // Deliberately not a valid encoded envelope: if ExtractTitleAsync attempted to parse this as one, it
    // would either throw or return garbage. A plain-category photo must short-circuit before that happens.
    byte[] notAnEnvelope = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    var photo = new Data(1, notAnEnvelope, "image/jpeg", DataCategory.PersonMainPhoto);

    var title = await GedcomPhotoResidue.ExtractTitleAsync(photo, _Token);

    title.Should().BeNull();
  }

  [Fact]
  public async Task ExtractTitleAsync_ReturnsNullForANullPhoto()
  {
    var title = await GedcomPhotoResidue.ExtractTitleAsync(null, _Token);

    title.Should().BeNull();
  }

  [Fact]
  public async Task ExtractFileNameAsync_ReturnsTheFileValue()
  {
    byte[] file = [1];
    var content = GedcomPhotoResidue.Encode(file, Residual(("FILE", "scan.pdf")));
    var attachment = new Data(1, content, "application/pdf", DataCategory.PersonAttachment);

    var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(attachment, _Token);

    fileName.Should().Be("scan.pdf");
  }

  [Fact]
  public async Task ExtractFileNameAsync_ReturnsNullForANonAttachmentCategory_WithoutAttemptingToParse()
  {
    // Deliberately not a valid encoded envelope: if ExtractFileNameAsync attempted to parse this as one,
    // it would either throw or return garbage. A non-attachment category must short-circuit before that.
    byte[] notAnEnvelope = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    var photo = new Data(1, notAnEnvelope, "image/jpeg", DataCategory.PersonMainPhoto);

    var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(photo, _Token);

    fileName.Should().BeNull();
  }

  [Fact]
  public async Task ExtractFileNameAsync_ReturnsNullForANullAttachment()
  {
    var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(null, _Token);

    fileName.Should().BeNull();
  }
}
