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
}
