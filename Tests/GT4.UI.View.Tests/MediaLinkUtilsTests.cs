using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class MediaLinkUtilsTests
{
  [Fact]
  public void KnownId_IsRewrittenToItsDataUri()
  {
    var html = "<p><img src=\"media:5\" alt=\"caption\" /></p>";
    var sources = new Dictionary<int, string> { [5] = "data:image/png;base64,AAA=" };

    var result = MediaLinkUtils.RewriteMediaSources(html, sources);

    result.Should().Be("<p><img src=\"data:image/png;base64,AAA=\" alt=\"caption\" /></p>");
  }

  [Fact]
  public void UnknownId_IsLeftUntouched()
  {
    var html = "<p><img src=\"media:999\" alt=\"caption\" /></p>";
    var sources = new Dictionary<int, string> { [5] = "data:image/png;base64,AAA=" };

    var result = MediaLinkUtils.RewriteMediaSources(html, sources);

    result.Should().Be(html);
  }

  [Fact]
  public void EmptySources_LeavesHtmlUntouched()
  {
    var html = "<p><img src=\"media:5\" /></p>";

    var result = MediaLinkUtils.RewriteMediaSources(html, new Dictionary<int, string>());

    result.Should().Be(html);
  }

  [Fact]
  public void MultipleReferences_AreEachRewritten()
  {
    var html = "<img src=\"media:1\" /><img src=\"media:2\" />";
    var sources = new Dictionary<int, string> { [1] = "data:image/png;base64,AAA=", [2] = "data:image/jpeg;base64,BBB=" };

    var result = MediaLinkUtils.RewriteMediaSources(html, sources);

    result.Should().Be("<img src=\"data:image/png;base64,AAA=\" /><img src=\"data:image/jpeg;base64,BBB=\" />");
  }

  [Fact]
  public void NonMediaSrc_IsLeftUntouched()
  {
    var html = "<img src=\"https://example.com/x.png\" />";

    var result = MediaLinkUtils.RewriteMediaSources(html, new Dictionary<int, string> { [1] = "data:image/png;base64,AAA=" });

    result.Should().Be(html);
  }

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
