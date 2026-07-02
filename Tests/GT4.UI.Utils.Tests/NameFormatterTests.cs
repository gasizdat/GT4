using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class NameFormatterTests
{
  private static NameFormatter CreateFormatter(
    string commonTemplate = "FF PP LL",
    string fullTemplate = "LL FF PP",
    string initialsTemplate = "FF. PP. LL",
    string shortTemplate = "FF. PP. LL")
  {
    var common = new Mock<ISettingEditor>();
    common.SetupGet(s => s.Value).Returns(commonTemplate);

    var full = new Mock<ISettingEditor>();
    full.SetupGet(s => s.Value).Returns(fullTemplate);

    var initials = new Mock<ISettingEditor>();
    initials.SetupGet(s => s.Value).Returns(initialsTemplate);

    var shortFmt = new Mock<ISettingEditor>();
    shortFmt.SetupGet(s => s.Value).Returns(shortTemplate);

    return new NameFormatter(common.Object, full.Object, initials.Object, shortFmt.Object);
  }

  private static PersonInfo Person(params (string value, NameType type)[] names)
  {
    var nameArray = names
      .Select((n, i) => new Name(i, n.value, n.type, null))
      .ToArray();
    return new PersonInfo(0, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Unknown, nameArray, null);
  }

  [Fact]
  public void CommonFormat_AllParts_RendersInOrder()
  {
    var formatter = CreateFormatter(commonTemplate: "FF PP LL");
    var person = Person(
      ("Ivan", NameType.FirstName | NameType.MaleDeclension),
      ("Ivanovich", NameType.Patronymic | NameType.MaleDeclension),
      ("Ivanov", NameType.LastName | NameType.MaleDeclension));

    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("Ivan Ivanovich Ivanov");
  }

  [Fact]
  public void FullFormat_LastNameFirst()
  {
    var formatter = CreateFormatter(fullTemplate: "LL FF PP");
    var person = Person(
      ("Ivan", NameType.FirstName | NameType.MaleDeclension),
      ("Ivanovich", NameType.Patronymic | NameType.MaleDeclension),
      ("Ivanov", NameType.LastName | NameType.MaleDeclension));

    formatter.ToString(person, NameFormat.FullPersonName).Should().Be("Ivanov Ivan Ivanovich");
  }

  [Fact]
  public void InitialsFormat_ShortensFirstAndPatronymic()
  {
    var formatter = CreateFormatter(initialsTemplate: "FF. PP. LL");
    var person = Person(
      ("Ivan", NameType.FirstName | NameType.MaleDeclension),
      ("Ivanovich", NameType.Patronymic | NameType.MaleDeclension),
      ("Ivanov", NameType.LastName | NameType.MaleDeclension));

    formatter.ToString(person, NameFormat.PersonInitials).Should().Be("I. I. Ivanov");
  }

  [Fact]
  public void FamilyNameFormat_RendersFamilyName()
  {
    var formatter = CreateFormatter(commonTemplate: "FN");
    var person = Person(
      ("Smiths", NameType.FamilyName),
      ("John", NameType.FirstName | NameType.MaleDeclension));

    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("Smiths");
  }

  [Fact]
  public void MissingPatronymic_ProducesExtraSpace()
  {
    var formatter = CreateFormatter(commonTemplate: "FF PP LL");
    var person = Person(
      ("Anna", NameType.FirstName | NameType.FemaleDeclension),
      ("Ivanova", NameType.LastName | NameType.FemaleDeclension));

    // PP is empty when no patronymic → two consecutive spaces remain in the output
    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("Anna  Ivanova");
  }

  [Fact]
  public void EmptyPerson_NoNames_OnlySpaces()
  {
    var formatter = CreateFormatter(commonTemplate: "FF PP LL");
    var person = Person();

    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("  ");
  }

  [Fact]
  public void MultipleFirstNames_JoinedBySpace()
  {
    var formatter = CreateFormatter(commonTemplate: "FF LL");
    var person = Person(
      ("John", NameType.FirstName | NameType.MaleDeclension),
      ("James", NameType.FirstName | NameType.MaleDeclension),
      ("Smith", NameType.LastName | NameType.MaleDeclension));

    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("John James Smith");
  }

  [Fact]
  public void InitialUppercases_FirstCharacter()
  {
    var formatter = CreateFormatter(initialsTemplate: "FF.");
    var person = Person(("anna", NameType.FirstName | NameType.FemaleDeclension));

    formatter.ToString(person, NameFormat.PersonInitials).Should().Be("A.");
  }

  [Fact]
  public void FemaleNameWithFemaleDeclension_MatchedByFirstNameType()
  {
    var formatter = CreateFormatter(commonTemplate: "FF LL");
    var person = Person(
      ("Maria", NameType.FirstName | NameType.FemaleDeclension),
      ("Petrova", NameType.LastName | NameType.FemaleDeclension));

    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("Maria Petrova");
  }

  [Fact]
  public void MaleDeclensionNameNotMatchedByFemaleFilter()
  {
    // Both male and female declension names are returned for "FF" because HasFlag(NameType.FirstName) is true
    var formatter = CreateFormatter(commonTemplate: "FF");
    var person = Person(
      ("Иван", NameType.FirstName | NameType.MaleDeclension),
      ("Ивановна", NameType.Patronymic | NameType.FemaleDeclension));

    // Only first names (not patronymics) are in the FF slot
    formatter.ToString(person, NameFormat.CommonPersonName).Should().Be("Иван");
  }
}
