using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class FileNameUtilsTests
{
  [Theory]
  [InlineData("report.pdf", "report.pdf")]
  // The set is fixed rather than Path.GetInvalidFileNameChars(), which on Unix is only '\0' and '/', so
  // these all sanitize identically no matter which platform produced the name.
  [InlineData("Smith: b.1900?", "Smith_ b.1900_")]
  [InlineData("a<b>c\"d|e*f", "a_b_c_d_e_f")]
  [InlineData("re\tport", "re_port")]
  [InlineData("..\\..\\etc", ".._.._etc")]
  public void Sanitize_ReplacesWhatNoFileSystemAccepts(string name, string expected) =>
    FileNameUtils.Sanitize(name, "fallback").Should().Be(expected);

  [Theory]
  [InlineData("")]
  [InlineData("..")]
  [InlineData("   ")]
  public void Sanitize_FallsBackWhenNothingUsableIsLeft(string name) =>
    FileNameUtils.Sanitize(name, "fallback").Should().Be("fallback");
}
