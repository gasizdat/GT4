using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.View.Tests;

public class NameTypeFormatterTests
{
  private readonly NameTypeFormatter _formatter = new();

  private static void SetEn() => Language.Current = Language.EN;
  private static void SetRu() => Language.Current = Language.RU;

  [Theory]
  [InlineData(NameType.FirstName, "First name")]
  [InlineData(NameType.Patronymic, "Patronymic")]
  [InlineData(NameType.LastName, "Last name")]
  [InlineData(NameType.FamilyName, "Family name")]
  public void EN_BaseTypes_ReturnCorrectString(NameType type, string expected)
  {
    SetEn();
    _formatter.ToString(type).Should().Be(expected);
  }

  [Theory]
  [InlineData(NameType.FirstName, "Имя")]
  [InlineData(NameType.Patronymic, "Отчество")]
  [InlineData(NameType.LastName, "Фамилия")]
  [InlineData(NameType.FamilyName, "Родовое имя")]
  public void RU_BaseTypes_ReturnCorrectString(NameType type, string expected)
  {
    SetRu();
    _formatter.ToString(type).Should().Be(expected);
  }

  [Theory]
  [InlineData(NameType.FirstName | NameType.MaleDeclension, "First name")]
  [InlineData(NameType.FirstName | NameType.FemaleDeclension, "First name")]
  [InlineData(NameType.LastName | NameType.MaleDeclension, "Last name")]
  [InlineData(NameType.LastName | NameType.FemaleDeclension, "Last name")]
  [InlineData(NameType.Patronymic | NameType.MaleDeclension, "Patronymic")]
  [InlineData(NameType.Patronymic | NameType.FemaleDeclension, "Patronymic")]
  public void EN_WithDeclensionFlags_StripsToBaseType(NameType type, string expected)
  {
    SetEn();
    _formatter.ToString(type).Should().Be(expected);
  }

  [Theory]
  [InlineData(NameType.FirstName | NameType.MaleDeclension, "Имя")]
  [InlineData(NameType.LastName | NameType.FemaleDeclension, "Фамилия")]
  [InlineData(NameType.Patronymic | NameType.MaleDeclension, "Отчество")]
  public void RU_WithDeclensionFlags_StripsToBaseType(NameType type, string expected)
  {
    SetRu();
    _formatter.ToString(type).Should().Be(expected);
  }

  [Theory]
  [InlineData(NameType.AllNames)]
  public void UnknownType_ReturnsEmpty(NameType type)
  {
    SetEn();
    _formatter.ToString(type).Should().BeEmpty();
  }
}
