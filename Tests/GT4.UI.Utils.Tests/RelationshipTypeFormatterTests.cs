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
      ToConsanguinity(generation));

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
      ToConsanguinity(generation));

    Assert.Equal(expected, actual);
  }
}
