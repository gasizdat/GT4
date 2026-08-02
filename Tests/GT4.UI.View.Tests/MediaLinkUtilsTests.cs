using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class MediaLinkUtilsTests
{
  [Fact]
  public void ExtractReferencedIds_FindsAMediaLink()
  {
    var ids = MediaLinkUtils.ExtractReferencedIds("![caption](media:5)");

    ids.Should().BeEquivalentTo([5]);
  }

  [Fact]
  public void ExtractReferencedIds_FindsEachDistinctIdOnce()
  {
    var ids = MediaLinkUtils.ExtractReferencedIds("![a](media:1) and again ![a](media:1) and ![b](media:2)");

    ids.Should().BeEquivalentTo([1, 2]);
  }

  [Fact]
  public void ExtractReferencedIds_NoReferences_IsEmpty()
  {
    var ids = MediaLinkUtils.ExtractReferencedIds("Just some text, no links.");

    ids.Should().BeEmpty();
  }

  [Fact]
  public void ExtractReferencedIds_NullOrEmpty_IsEmpty()
  {
    MediaLinkUtils.ExtractReferencedIds(null).Should().BeEmpty();
    MediaLinkUtils.ExtractReferencedIds(string.Empty).Should().BeEmpty();
  }

  [Theory]
  [InlineData("Grandpa 50%", 50, "Grandpa")]
  [InlineData("Grandpa 100%", 100, "Grandpa")]
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
  [InlineData("Grandpa 150%")]
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
