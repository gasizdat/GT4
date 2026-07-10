using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class WildcardMatcherTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void EmptyOrNullPattern_MatchesAnything(string? pattern)
  {
    WildcardMatcher.IsMatch("Anything", pattern!).Should().BeTrue();
  }

  [Theory]
  [InlineData("oh", "John")]
  [InlineData("OH", "John")]
  [InlineData("john", "JOHN")]
  public void PlainPattern_MatchesAsCaseInsensitiveSubstring(string pattern, string input)
  {
    WildcardMatcher.IsMatch(input, pattern).Should().BeTrue();
  }

  [Fact]
  public void PlainPattern_NoSubstringMatch_ReturnsFalse()
  {
    WildcardMatcher.IsMatch("John", "xyz").Should().BeFalse();
  }

  [Theory]
  [InlineData("J*n", "Jordan")]
  [InlineData("*ohn", "John")]
  [InlineData("Jo*", "John")]
  public void StarWildcard_MatchesAnyRunOfCharacters(string pattern, string input)
  {
    WildcardMatcher.IsMatch(input, pattern).Should().BeTrue();
  }

  [Theory]
  [InlineData("J?hn", "John")]
  [InlineData("J?hn", "Jhn", false)]
  [InlineData("J?hn", "Joohn", false)]
  public void QuestionMarkWildcard_MatchesExactlyOneCharacter(string pattern, string input, bool expected = true)
  {
    WildcardMatcher.IsMatch(input, pattern).Should().Be(expected);
  }

  [Fact]
  public void OtherRegexMetacharacters_AreTreatedLiterally()
  {
    WildcardMatcher.IsMatch("a.b", "a.b").Should().BeTrue();
    WildcardMatcher.IsMatch("axb", "a.b").Should().BeFalse();
  }
}
