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
  private static void SetDe() => Language.Current = Language.DE;
  private static void SetEs() => Language.Current = Language.ES;

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

  // German builds generations by repeating the "Ur" prefix, so the composed label is a single word
  // and the past-max fallback keeps the same numeral form En and Ru already use.
  [Theory]
  [InlineData(null, "Elternteil")]
  [InlineData(1, "Elternteil")]
  [InlineData(2, "Großelternteil")]
  [InlineData(4, "Ururgroßelternteil")]
  [InlineData(40, "38-Urgroßelternteil")]
  public void DE_UnknownSex_Parent(int? generation, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.Parent,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      generation.HasValue ? Consanguinity.Zero : null);

    Assert.Equal(expected, actual);
  }

  // Spanish names each level with its own prefix bound to the stem rather than by repeating one
  // prefix, and it names only two of them before falling back to the numeral form.
  [Theory]
  [InlineData(null, "Progenitor")]
  [InlineData(1, "Progenitor")]
  [InlineData(2, "Abuelo o abuela")]
  [InlineData(3, "Bisabuelo o bisabuela")]
  [InlineData(4, "Tatarabuelo o tatarabuela")]
  [InlineData(40, "38-tatarabuelo o tatarabuela")]
  public void ES_UnknownSex_Parent(int? generation, string expected)
  {
    SetEs();
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
  [InlineData(-1, "Kind")]
  [InlineData(-2, "Enkelkind")]
  [InlineData(-4, "Ururenkelkind")]
  [InlineData(-15, "13-Urenkelkind")]
  public void DE_UnknownSex_Child(int? generation, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // "tatara" runs straight into "nieto" where it contracted with "abuelo".
  [Theory]
  [InlineData(-1, "Hijo o hija")]
  [InlineData(-2, "Nieto o nieta")]
  [InlineData(-3, "Bisnieto o bisnieta")]
  [InlineData(-4, "Tataranieto o tataranieta")]
  [InlineData(-15, "13-tataranieto o tataranieta")]
  public void ES_UnknownSex_Child(int? generation, string expected)
  {
    SetEs();
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

  // "Onkel oder Tante" is the case the shared En/Ru casing pass would have broken: German keeps the
  // capital on every noun, so only the capitals introduced by prefixing may be folded away.
  [Theory]
  [InlineData(1, "Onkel oder Tante")]
  [InlineData(2, "Großonkel oder Großtante")]
  [InlineData(4, "Ururgroßonkel oder Großtante")]
  [InlineData(14, "12-Urgroßonkel oder Großtante")]
  public void DE_UnknownSex_UncleAunt(int generation, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(generation) + Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  // The case a prefix wrapped around the whole phrase would get wrong twice over: the stem sits in
  // the middle of "tío abuelo", and the disjunction carries a second stem that must be prefixed too.
  [Theory]
  [InlineData(1, "Tío o tía")]
  [InlineData(2, "Tío abuelo o tía abuela")]
  [InlineData(3, "Tío bisabuelo o tía bisabuela")]
  [InlineData(4, "Tío tatarabuelo o tía tatarabuela")]
  [InlineData(14, "12-tío tatarabuelo o tía tatarabuela")]
  public void ES_UnknownSex_UncleAunt(int generation, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(generation) + Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  // The descendant mirror of the case above, and the only one where a two-stem disjunction meets the
  // seam that does not contract.
  [Theory]
  [InlineData(-2, "Sobrino nieto o sobrina nieta")]
  [InlineData(-3, "Sobrino bisnieto o sobrina bisnieta")]
  [InlineData(-4, "Sobrino tataranieto o sobrina tataranieta")]
  [InlineData(-14, "12-sobrino tataranieto o sobrina tataranieta")]
  public void ES_UnknownSex_GrandNephewNiece(int generation, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      Consanguinity.Sibling);

    Assert.Equal(expected, actual);
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

  [Theory]
  [InlineData(0, 2, "Cousin oder Cousine")]
  [InlineData(0, 3, "Cousin oder Cousine zweiten Grades")]
  [InlineData(0, 56, "Cousin oder Cousine 55. Grades")]
  [InlineData(1, 3, "Cousin oder Cousine, eine Generation entfernt")]
  [InlineData(1, 4, "Cousin oder Cousine zweiten Grades, eine Generation entfernt")]
  [InlineData(2, 4, "Cousin oder Cousine, zwei Generationen entfernt")]
  [InlineData(2, 5, "Cousin oder Cousine zweiten Grades, zwei Generationen entfernt")]
  [InlineData(15, 25, "Cousin oder Cousine 9. Grades, 15 Generationen entfernt")]
  [InlineData(1, 1, "Unsupported or wrong relationship: Type=Child, Sex=Unknown, G1, C1")]
  public void DE_UnknownSex_Cousin(int generation, int consanguinity, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(consanguinity));

    Assert.Equal(expected, actual);
  }

  // Spanish has no native counterpart to "removed", so the offset is spelled out in generations.
  [Theory]
  [InlineData(0, 2, "Primo o prima")]
  [InlineData(0, 3, "Primo o prima de segundo grado")]
  [InlineData(0, 56, "Primo o prima de 55.º grado")]
  [InlineData(1, 3, "Primo o prima, una generación de diferencia")]
  [InlineData(1, 4, "Primo o prima de segundo grado, una generación de diferencia")]
  [InlineData(2, 4, "Primo o prima, dos generaciones de diferencia")]
  [InlineData(2, 5, "Primo o prima de segundo grado, dos generaciones de diferencia")]
  [InlineData(15, 25, "Primo o prima de 9.º grado, 15 generaciones de diferencia")]
  [InlineData(1, 1, "Unsupported or wrong relationship: Type=Child, Sex=Unknown, G1, C1")]
  public void ES_UnknownSex_Cousin(int generation, int consanguinity, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(consanguinity));

    Assert.Equal(expected, actual);
  }

  // Unlike English, German genders the cousin term.
  [Theory]
  [InlineData(BiologicalSex.Female, "Cousine zweiten Grades")]
  [InlineData(BiologicalSex.Male, "Cousin zweiten Grades")]
  public void DE_Cousin_IsGendered(BiologicalSex sex, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      sex,
      Generation.Zero,
      ToConsanguinity(3));

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(BiologicalSex.Female, "Prima de segundo grado")]
  [InlineData(BiologicalSex.Male, "Primo de segundo grado")]
  public void ES_Cousin_IsGendered(BiologicalSex sex, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      sex,
      Generation.Zero,
      ToConsanguinity(3));

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(BiologicalSex.Female)]
  [InlineData(BiologicalSex.Male)]
  [InlineData(BiologicalSex.Unknown)]
  public void EN_Cousin_StaysSexInvariant(BiologicalSex sex)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Child,
      sex,
      Generation.Zero,
      ToConsanguinity(3));

    Assert.Equal("Second cousin", actual);
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

  [Theory]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Male, "Schwiegervater")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Female, "Schwiegermutter")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Unknown, "Schwiegerelternteil")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Male, "Schwiegervater")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Female, "Schwiegermutter")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Unknown, "Schwiegerelternteil")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Male, "Schwiegervater")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Female, "Schwiegermutter")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Unknown, "Schwiegerelternteil")]
  public void DE_InLawParent(RelationshipType type, BiologicalSex inLawSex, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      type,
      inLawSex,
      Generation.Parent,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Male, "Suegro")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Female, "Suegra")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Unknown, "Suegro o suegra")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Male, "Suegro")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Female, "Suegra")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Unknown, "Suegro o suegra")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Male, "Suegro")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Female, "Suegra")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Unknown, "Suegro o suegra")]
  public void ES_InLawParent(RelationshipType type, BiologicalSex inLawSex, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      type,
      inLawSex,
      Generation.Parent,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // A sibling's spouse (same generation, sibling consanguinity) is formatted
  // as a sibling-in-law, resolved by the spouse's own sex.
  [Theory]
  [InlineData(BiologicalSex.Female, "Sister-in-law")]
  [InlineData(BiologicalSex.Male, "Brother-in-law")]
  [InlineData(BiologicalSex.Unknown, "Sibling-in-law")]
  public void EN_SiblingSpouse(BiologicalSex spouseSex, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Spouse,
      spouseSex,
      Generation.Zero,
      Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(BiologicalSex.Female, "Невестка")]
  [InlineData(BiologicalSex.Male, "Зять")]
  [InlineData(BiologicalSex.Unknown, "Супруг(а) сестры или брата")]
  public void RU_SiblingSpouse(BiologicalSex spouseSex, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.Spouse,
      spouseSex,
      Generation.Zero,
      Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(BiologicalSex.Female, "Schwägerin")]
  [InlineData(BiologicalSex.Male, "Schwager")]
  [InlineData(BiologicalSex.Unknown, "Schwager oder Schwägerin")]
  public void DE_SiblingSpouse(BiologicalSex spouseSex, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.Spouse,
      spouseSex,
      Generation.Zero,
      Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(BiologicalSex.Female, "Cuñada")]
  [InlineData(BiologicalSex.Male, "Cuñado")]
  [InlineData(BiologicalSex.Unknown, "Cuñado o cuñada")]
  public void ES_SiblingSpouse(BiologicalSex spouseSex, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.Spouse,
      spouseSex,
      Generation.Zero,
      Consanguinity.Sibling);

    Assert.Equal(expected, actual);
  }

  // A direct step-sibling (same generation, no consanguinity) is gendered by
  // the step-sibling's own sex.
  [Theory]
  [InlineData(BiologicalSex.Female, "Stepsister")]
  [InlineData(BiologicalSex.Male, "Stepbrother")]
  [InlineData(BiologicalSex.Unknown, "Stepsibling")]
  public void EN_StepSibling(BiologicalSex sex, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.StepSibling,
      sex,
      Generation.Zero,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData(BiologicalSex.Female, "Сводная сестра")]
  [InlineData(BiologicalSex.Male, "Сводный брат")]
  [InlineData(BiologicalSex.Unknown, "Сводный брат или сестра")]
  public void RU_StepSibling(BiologicalSex sex, string expected)
  {
    SetRu();
    var actual = _formatter.ToString(
      RelationshipType.StepSibling,
      sex,
      Generation.Zero,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // "Stief" + "Bruder oder Schwester" exercises both halves of the German casing rule in one label:
  // the prefixed noun folds, the one after the space does not.
  [Theory]
  [InlineData(BiologicalSex.Female, "Stiefschwester")]
  [InlineData(BiologicalSex.Male, "Stiefbruder")]
  [InlineData(BiologicalSex.Unknown, "Stiefbruder oder Schwester")]
  public void DE_StepSibling(BiologicalSex sex, string expected)
  {
    SetDe();
    var actual = _formatter.ToString(
      RelationshipType.StepSibling,
      sex,
      Generation.Zero,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // Spanish marks a step-sibling with the "-astro" suffix on the noun itself, which no template
  // wrapped around "hermano" can produce, so the step rows spell the whole word.
  [Theory]
  [InlineData(BiologicalSex.Female, "Hermanastra")]
  [InlineData(BiologicalSex.Male, "Hermanastro")]
  [InlineData(BiologicalSex.Unknown, "Hermanastro o hermanastra")]
  public void ES_StepSibling(BiologicalSex sex, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.StepSibling,
      sex,
      Generation.Zero,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // The adoptive adjective trails the noun in Spanish, so a prefix bound to the whole phrase would
  // land on "adoptivo" instead of the stem.
  [Theory]
  [InlineData(2, "Abuelo adoptivo")]
  [InlineData(4, "Tatarabuelo adoptivo")]
  public void ES_AdoptiveAncestor_KeepsTrailingAdjective(int generation, string expected)
  {
    SetEs();
    var actual = _formatter.ToString(
      RelationshipType.AdoptiveParent,
      BiologicalSex.Male,
      ToGeneration(generation),
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }
}
