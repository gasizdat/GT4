using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Pins the pure string logic behind <see cref="MediaStorePatterns"/>: the relative-path
/// normalization and the wildcard-to-SQL-LIKE translation AndroidFileSystem builds its
/// MediaStore WHERE clauses from.
/// </summary>
public sealed class MediaStorePatternsTests
{
  [Theory]
  [InlineData("", "")]
  [InlineData("Documents/GT4", "Documents/GT4/")]
  [InlineData("Documents/GT4/", "Documents/GT4/")]
  public void EnsureTrailingSlash_AppendsExactlyOneSeparator(string input, string expected)
  {
    MediaStorePatterns.EnsureTrailingSlash(input).Should().Be(expected);
  }

  [Theory]
  [InlineData("version-*.gt4", "version-%.gt4")]
  [InlineData("a?b", "a_b")]
  [InlineData("file_name*", "file\\_name%")]
  [InlineData("100%", "100\\%")]
  [InlineData("back\\slash", "back\\\\slash")]
  [InlineData("plain.gt4", "plain.gt4")]
  public void ToLikePattern_TranslatesWildcardsAndEscapesLikeMetacharacters(string input, string expected)
  {
    MediaStorePatterns.ToLikePattern(input).Should().Be(expected);
  }
}
