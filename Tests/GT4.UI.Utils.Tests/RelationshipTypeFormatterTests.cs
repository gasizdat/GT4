using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters;
using Xunit;


namespace GT4.UI.Utils.Tests;

public class RelationshipTypeFormatterTests
{
  private readonly RelationshipTypeFormatter _formatter = new();

  private static Generation? ToGeneration(int? generation) =>
    generation.HasValue ? new Generation(generation.Value) : null;

  private static Consanguinity? ToConsanguinity(int? consanguinity) =>
    consanguinity.HasValue ? new Consanguinity(consanguinity.Value) : null;

  private static void SetEn() => Language.Current = Language.EN;
  private static void SetRu() => Language.Current = Language.RU;

  [Theory]
  [InlineData(null, "Parent")]
  [InlineData(1, "Parent")]
  [InlineData(2, "Grandparent")]
  [InlineData(4, "Great-great-grandparent")]
  [InlineData(40, "38-great-grandparent")]
  public void EN_UnknownSex_Parent(int? generation, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Parent,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      generation.HasValue ? Consanguinity.Zero : null);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(null, "Родитель")]
  [InlineData(1, "Родитель")]
  [InlineData(2, "Дедушка или бабушка")]
  [InlineData(4, "Пра-пра-дедушка или бабушка")]
  [InlineData(40, "38-пра-дедушка или бабушка")]
  public void RU_UnknownSex_Parent(int? generation, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.Parent,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      generation.HasValue ? Consanguinity.Zero : null);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(null, "Child")]
  [InlineData(-1, "Child")]
  [InlineData(-2, "Grandchild")]
  [InlineData(-4, "Great-great-grandchild")]
  [InlineData(-15, "13-great-grandchild")]
  public void EN_UnknownSex_Child(int? generation, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      generation.HasValue ? Consanguinity.Zero : null);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(-1, "Ребенок")]
  [InlineData(-2, "Внук или внучка")]
  [InlineData(-4, "Пра-пра-внук или внучка")]
  [InlineData(-55, "53-пра-внук или внучка")]
  public void RU_UnknownSex_Child(int? generation, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(1, "Uncle or aunt")]
  [InlineData(2, "Grand uncle or aunt")]
  [InlineData(4, "Great-great-grand uncle or aunt")]
  [InlineData(14, "12-great-grand uncle or aunt")]
  public void EN_UnknownSex_UncleAunt(int generation, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(generation) + Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(1, "Дядя или тётя")]
  [InlineData(2, "Двоюродный дедушка или бабушка")]
  [InlineData(4, "Двоюродный пра-пра-дедушка или бабушка")]
  [InlineData(14, "Двоюродный 12-пра-дедушка или бабушка")]
  public void RU_UnknownSex_UncleAunt(int generation, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(generation) + Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void RU_Female_UncleAunt_Regression()
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Female,
      ToGeneration(4),
      ToConsanguinity(5));

    Assert.Equal("Двоюродная пра-пра-бабушка", actual);
  }

  [Theory]
  [InlineData(0, 2, "Cousin")]
  [InlineData(0, 3, "Second cousin")]
  [InlineData(0, 56, "55th cousin")]
  [InlineData(1, 3, "Cousin once removed")]
  [InlineData(1, 4, "Second cousin once removed")]
  [InlineData(2, 4, "Cousin twice removed")]
  [InlineData(2, 5, "Second cousin twice removed")]
  [InlineData(15, 25, "9th cousin 15x removed")]
  [InlineData(1, 1, "Unsupported or wrong relationship: Type=Child, Sex=Unknown, G1, C1")]
  public void EN_UnknownSex_Cousin(int generation, int consanguinity, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(consanguinity));

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(0, 2, "Двоюродный брат или сестра")]
  [InlineData(0, 3, "Троюродный брат или сестра")]
  [InlineData(0, 56, "56-юродный брат или сестра")]
  [InlineData(1, 3, "Двоюродный дядя или тётя")]
  [InlineData(1, 4, "Троюродный дядя или тётя")]
  [InlineData(1, 12, "11-юродный дядя или тётя")]
  [InlineData(2, 4, "Троюродный дедушка или бабушка")]
  [InlineData(2, 5, "Четвероюродный дедушка или бабушка")]
  [InlineData(2, 25, "24-юродный дедушка или бабушка")]
  [InlineData(3, 5, "Троюродный пра-дедушка или бабушка")]
  [InlineData(3, 7, "5-юродный пра-дедушка или бабушка")]
  [InlineData(13, 16, "Четвероюродный 11-пра-дедушка или бабушка")]
  [InlineData(10, 16, "7-юродный 8-пра-дедушка или бабушка")]
  [InlineData(11, 11, "Unsupported or wrong relationship: Type=Child, Sex=Unknown, G11, C11")]
  public void RU_UnknownSex_Cousin(int generation, int consanguinity, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(consanguinity));

    Assert.Equal(expected, actual);
  }

  // In-law parents: the relationship Type carries the spouse's sex
  // (Husband/Wife/Spouse), while the BiologicalSex argument is the in-law
  // parent's own sex (father-in-law vs mother-in-law).
  [Theory]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Male, "Father-in-law")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Female, "Mother-in-law")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Unknown, "In-law")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Male, "Father-in-law")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Female, "Mother-in-law")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Unknown, "In-law")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Male, "In-law")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Female, "In-law")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Unknown, "In-law")]
  public void EN_InLawParent(RelationshipType type, BiologicalSex inLawSex, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      type,
      inLawSex,
      Generation.Parent,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Male, "Свёкр")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Female, "Свекровь")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Unknown, "Свойственник")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Male, "Тесть")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Female, "Тёща")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Unknown, "Свойственник")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Male, "Свойственник")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Female, "Свойственница")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Unknown, "Свойственник")]
  public void RU_InLawParent(RelationshipType type, BiologicalSex inLawSex, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      type,
      inLawSex,
      Generation.Parent,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }
}
