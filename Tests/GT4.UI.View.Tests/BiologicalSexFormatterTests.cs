using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.View.Tests;

public class BiologicalSexFormatterTests
{
  private readonly BiologicalSexFormatter _formatter = new();

  private static void SetEn() => Language.Current = Language.EN;
  private static void SetRu() => Language.Current = Language.RU;

  [Theory]
  [InlineData(BiologicalSex.Male, "♂ Man")]
  [InlineData(BiologicalSex.Female, "♀ Woman")]
  [InlineData(BiologicalSex.Unknown, "⚧️ Human")]
  public void EN_KnownValues_ReturnLocalizedString(BiologicalSex sex, string expected)
  {
    SetEn();
    _formatter.ToString(sex).Should().Be(expected);
  }

  [Theory]
  [InlineData(BiologicalSex.Male, "♂ Мужчина")]
  [InlineData(BiologicalSex.Female, "♀ Женщина")]
  [InlineData(BiologicalSex.Unknown, "⚧️ Человек")]
  public void RU_KnownValues_ReturnLocalizedString(BiologicalSex sex, string expected)
  {
    SetRu();
    _formatter.ToString(sex).Should().Be(expected);
  }

  [Fact]
  public void EN_NullValue_ReturnsUnknownString()
  {
    SetEn();
    _formatter.ToString(null).Should().Be("⚧️ Human");
  }

  [Fact]
  public void RU_NullValue_ReturnsUnknownString()
  {
    SetRu();
    _formatter.ToString(null).Should().Be("⚧️ Человек");
  }
}
