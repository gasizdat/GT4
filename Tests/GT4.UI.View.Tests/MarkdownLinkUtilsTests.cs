using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class MarkdownLinkUtilsTests
{
  // The whole reason both directions sit in one class: a url a writer builds has to be one the reader
  // parses back. Nothing else in the app checks that pairing.
  [Fact]
  public void PersonUrl_RoundTripsThroughItsParser()
  {
    var url = MarkdownLinkUtils.PersonUrl(123);

    MarkdownLinkUtils.TryParsePersonId(url, out var personId).Should().BeTrue();
    personId.Should().Be(123);
  }

  [Fact]
  public void AttachmentUrl_RoundTripsThroughItsParser()
  {
    var url = MarkdownLinkUtils.AttachmentUrl(7);

    MarkdownLinkUtils.TryParseAttachmentId(url, out var attachmentId).Should().BeTrue();
    attachmentId.Should().Be(7);
  }

  [Fact]
  public void MediaUrl_RoundTripsThroughItsParser()
  {
    var url = MarkdownLinkUtils.MediaUrl(5);

    MarkdownLinkUtils.TryParseMediaId(url, out var mediaId).Should().BeTrue();
    mediaId.Should().Be(5);
  }

  // One scheme's url must not read as another's -- a tapped attachment link raising PersonLinkTapped
  // would open the wrong thing entirely.
  [Fact]
  public void EachParser_RejectsTheOtherSchemesUrls()
  {
    var personUrl = MarkdownLinkUtils.PersonUrl(5);
    var mediaUrl = MarkdownLinkUtils.MediaUrl(5);

    MarkdownLinkUtils.TryParseMediaId(personUrl, out _).Should().BeFalse();
    MarkdownLinkUtils.TryParseAttachmentId(personUrl, out _).Should().BeFalse();
    MarkdownLinkUtils.TryParsePersonId(mediaUrl, out _).Should().BeFalse();
  }

  // Why this is TryParse and not Parse: everything here is reachable from what a user types into a
  // biography, and the id run is unbounded.
  [Theory]
  [InlineData("https://example.test/a.png")]
  [InlineData("media:")]
  [InlineData("media:99999999999")]
  [InlineData("media:12abc")]
  [InlineData("Look at media:5 for that")]
  public void TryParseMediaId_AnythingElse_YieldsNothing(string url)
  {
    MarkdownLinkUtils.TryParseMediaId(url, out var id).Should().BeFalse();
    id.Should().Be(0);
  }

  [Theory]
  [InlineData("Grandpa 50%", 50, "Grandpa")]
  [InlineData("Grandpa 100%", 100, "Grandpa")]
  [InlineData("Grandpa 150%", 150, "Grandpa")]
  [InlineData("Grandpa 299%", 299, "Grandpa")]
  [InlineData("Grandpa 1%", 1, "Grandpa")]
  [InlineData("A whole caption in words 25%", 25, "A whole caption in words")]
  [InlineData("50%", 50, "")]
  public void ParseImageDescription_TrailingPercentage_IsAWidth(string description, int expected, string caption)
  {
    var parsed = MarkdownLinkUtils.ParseImageDescription(description);

    parsed.WidthPercent.Should().Be(expected);
    parsed.Caption.Should().Be(caption);
  }

  [Theory]
  [InlineData("Grandpa")]
  [InlineData("Grandpa 0%")]
  [InlineData("Grandpa 300%")]
  [InlineData("Grandpa 007%")]
  [InlineData("50% of the family")]
  [InlineData("Grandpa 50")]
  [InlineData("Grandpa50%")]
  [InlineData("")]
  public void ParseImageDescription_AnythingElse_StaysTheWholeCaption(string description)
  {
    var parsed = MarkdownLinkUtils.ParseImageDescription(description);

    parsed.WidthPercent.Should().BeNull();
    parsed.Caption.Should().Be(description);
  }
}
