using FluentAssertions;
using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomFamilyNameTests
{
  [Theory]
  [InlineData("Иванов", NameType.MaleDeclension, "Ивановы")]
  [InlineData("Иванова", NameType.FemaleDeclension, "Ивановы")]
  [InlineData("Соловьев", NameType.MaleDeclension, "Соловьевы")]
  [InlineData("Соловьева", NameType.FemaleDeclension, "Соловьевы")]
  [InlineData("Королёв", NameType.MaleDeclension, "Королёвы")]
  [InlineData("Королёва", NameType.FemaleDeclension, "Королёвы")]
  [InlineData("Пушкин", NameType.MaleDeclension, "Пушкины")]
  [InlineData("Пушкина", NameType.FemaleDeclension, "Пушкины")]
  [InlineData("Птицын", NameType.MaleDeclension, "Птицыны")]
  [InlineData("Птицына", NameType.FemaleDeclension, "Птицыны")]
  [InlineData("Достоевский", NameType.MaleDeclension, "Достоевские")]
  [InlineData("Достоевская", NameType.FemaleDeclension, "Достоевские")]
  [InlineData("Трубецкой", NameType.MaleDeclension, "Трубецкие")]
  [InlineData("Трубецкая", NameType.FemaleDeclension, "Трубецкие")]
  [InlineData("Толстой", NameType.MaleDeclension, "Толстые")]
  [InlineData("Толстая", NameType.FemaleDeclension, "Толстые")]
  [InlineData("Толстый", NameType.MaleDeclension, "Толстые")]
  [InlineData("Горький", NameType.MaleDeclension, "Горькие")]
  [InlineData("Горькая", NameType.FemaleDeclension, "Горькие")]
  [InlineData("Синий", NameType.MaleDeclension, "Синие")]
  [InlineData("Синяя", NameType.FemaleDeclension, "Синие")]
  public void Plural_PairsAGenderedSurnameWithItsFamily(string surname, NameType declension, string family)
  {
    GedcomFamilyName.Plural(surname, declension).Should().Be(family);
  }

  [Theory]
  [InlineData("Толстой", NameType.MaleDeclension, "Толстые")]
  [InlineData("Достоевский", NameType.MaleDeclension, "Достоевские")]
  [InlineData("Королёв", NameType.MaleDeclension, "Королёвы")]
  [InlineData("Королёва", NameType.FemaleDeclension, "Королёвы")]
  public void Plural_PairsADecomposedSurnameWithItsComposedFamily(string surname, NameType declension, string family)
  {
    GedcomFamilyName.Plural(surname, declension).Should().Be(family);
  }

  [Theory]
  [InlineData("Иванова", NameType.MaleDeclension)]
  [InlineData("Иванов", NameType.FemaleDeclension)]
  [InlineData("Иванов", (NameType)0)]
  [InlineData("Малина", NameType.MaleDeclension)]
  [InlineData("Черных", NameType.MaleDeclension)]
  [InlineData("Черных", NameType.FemaleDeclension)]
  [InlineData("Шевченко", NameType.FemaleDeclension)]
  [InlineData("Римский-Корсаков", NameType.MaleDeclension)]
  [InlineData("Иванова (Петрова)", NameType.FemaleDeclension)]
  [InlineData("ИВАНОВ", NameType.MaleDeclension)]
  [InlineData("Ivanov", NameType.MaleDeclension)]
  [InlineData("Ivanova", NameType.FemaleDeclension)]
  [InlineData("Білов", NameType.MaleDeclension)]
  [InlineData("Білова", NameType.FemaleDeclension)]
  [InlineData("Ґонтаров", NameType.MaleDeclension)]
  [InlineData("Іванова", NameType.FemaleDeclension)]
  [InlineData("Ов", NameType.MaleDeclension)]
  [InlineData("Ивановы", NameType.MaleDeclension)]
  [InlineData("Ивановы", NameType.FemaleDeclension)]
  [InlineData("Толстые", NameType.MaleDeclension)]
  [InlineData("Достоевские", NameType.FemaleDeclension)]
  public void Plural_LeavesAnyOtherSurnameUnpaired(string surname, NameType declension)
  {
    GedcomFamilyName.Plural(surname, declension).Should().BeNull();
  }
}
