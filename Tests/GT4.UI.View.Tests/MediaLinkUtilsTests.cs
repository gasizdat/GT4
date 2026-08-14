using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class MediaLinkUtilsTests
{
  [Theory]
  [InlineData("media:5", "media:", 5)]
  [InlineData("person:123", "person:", 123)]
  [InlineData("attachment:7", "attachment:", 7)]
  public void TryParseLinkId_AMatchingScheme_YieldsItsId(string url, string scheme, int expected)
  {
    MediaLinkUtils.TryParseLinkId(url, scheme, out var id).Should().BeTrue();
    id.Should().Be(expected);
  }

  // The last two are why this is TryParse and not Parse: an id too large for an int reaches it from
  // anything a user can type into a biography.
  [Theory]
  [InlineData("media:5", "person:")]
  [InlineData("https://example.test/a.png", "media:")]
  [InlineData("media:", "media:")]
  [InlineData("media:99999999999", "media:")]
  [InlineData("media:12abc", "media:")]
  public void TryParseLinkId_AnythingElse_YieldsNothing(string url, string scheme)
  {
    MediaLinkUtils.TryParseLinkId(url, scheme, out var id).Should().BeFalse();
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
    var parsed = MediaLinkUtils.ParseImageDescription(description);

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
    var parsed = MediaLinkUtils.ParseImageDescription(description);

    parsed.WidthPercent.Should().BeNull();
    parsed.Caption.Should().Be(description);
  }
}
