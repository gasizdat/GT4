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
  private static void SetFr() => Language.Current = Language.FR;

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
  [InlineData(4, "Пра-пра-дедушка или пра-пра-бабушка")]
  [InlineData(40, "38-пра-дедушка или 38-пра-бабушка")]
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
  [InlineData(40, "38-tatarabuelo o 38-tatarabuela")]
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

  // French repeats one prefix around the whole phrase the way En and Ru do, so it inherits the base
  // greatness pass unchanged; "arrière-" is what that prefix spells.
  [Theory]
  [InlineData(null, "Parent")]
  [InlineData(1, "Parent")]
  [InlineData(2, "Grand-parent")]
  [InlineData(4, "Arrière-arrière-grand-parent")]
  [InlineData(40, "38-arrière-grand-parent")]
  public void FR_UnknownSex_Parent(int? generation, string expected)
  {
    SetFr();
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
  [InlineData(-4, "Пра-пра-внук или пра-пра-внучка")]
  [InlineData(-55, "53-пра-внук или 53-пра-внучка")]
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
  [InlineData(-15, "13-tataranieto o 13-tataranieta")]
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
  [InlineData(-1, "Enfant")]
  [InlineData(-2, "Petit-enfant")]
  [InlineData(-4, "Arrière-arrière-petit-enfant")]
  [InlineData(-15, "13-arrière-petit-enfant")]
  public void FR_UnknownSex_Child(int? generation, string expected)
  {
    SetFr();
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
  [InlineData(2, "Двоюродный дедушка или двоюродная бабушка")]
  [InlineData(4, "Двоюродный пра-пра-дедушка или двоюродная пра-пра-бабушка")]
  [InlineData(14, "Двоюродный 12-пра-дедушка или двоюродная 12-пра-бабушка")]
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
  [InlineData(4, "Ururgroßonkel oder Ururgroßtante")]
  [InlineData(14, "12-Urgroßonkel oder 12-Urgroßtante")]
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
  [InlineData(14, "12-tío tatarabuelo o 12-tía tatarabuela")]
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
  [InlineData(-14, "12-sobrino tataranieto o 12-sobrina tataranieta")]
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

  // "Grand-oncle ou grand-tante" is the one French term where the base greatness pass reaches only
  // the first disjunct, the same shortfall German shows on "Ururgroßonkel oder Großtante" (#318).
  [Theory]
  [InlineData(1, "Oncle ou tante")]
  [InlineData(2, "Grand-oncle ou grand-tante")]
  [InlineData(4, "Arrière-arrière-grand-oncle ou arrière-arrière-grand-tante")]
  [InlineData(14, "12-arrière-grand-oncle ou 12-arrière-grand-tante")]
  public void FR_UnknownSex_UncleAunt(int generation, string expected)
  {
    SetFr();
    var actual = _formatter.ToString(
      RelationshipType.Sibling,
      BiologicalSex.Unknown,
      ToGeneration(generation),
      ToConsanguinity(generation) + Consanguinity.Sibling);

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
  [InlineData(0, 2, "Двоюродный брат или двоюродная сестра")]
  [InlineData(0, 3, "Троюродный брат или троюродная сестра")]
  [InlineData(0, 56, "56-юродный брат или 56-юродная сестра")]
  [InlineData(1, 3, "Двоюродный дядя или двоюродная тётя")]
  [InlineData(1, 4, "Троюродный дядя или троюродная тётя")]
  [InlineData(1, 12, "11-юродный дядя или 11-юродная тётя")]
  [InlineData(2, 4, "Троюродный дедушка или троюродная бабушка")]
  [InlineData(2, 5, "Четвероюродный дедушка или четвероюродная бабушка")]
  [InlineData(2, 25, "24-юродный дедушка или 24-юродная бабушка")]
  [InlineData(3, 5, "Троюродный пра-дедушка или троюродная пра-бабушка")]
  [InlineData(3, 7, "5-юродный пра-дедушка или 5-юродная пра-бабушка")]
  [InlineData(13, 16, "Четвероюродный 11-пра-дедушка или четвероюродная 11-пра-бабушка")]
  [InlineData(10, 16, "7-юродный 8-пра-дедушка или 7-юродная 8-пра-бабушка")]
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

  // French has no native counterpart to "removed" either, so the offset is spelled out in
  // generations.
  [Theory]
  [InlineData(0, 2, "Cousin ou cousine")]
  [InlineData(0, 3, "Cousin ou cousine au deuxième degré")]
  [InlineData(0, 56, "Cousin ou cousine au 55e degré")]
  [InlineData(1, 3, "Cousin ou cousine, à une génération d'écart")]
  [InlineData(1, 4, "Cousin ou cousine au deuxième degré, à une génération d'écart")]
  [InlineData(2, 4, "Cousin ou cousine, à deux générations d'écart")]
  [InlineData(2, 5, "Cousin ou cousine au deuxième degré, à deux générations d'écart")]
  [InlineData(15, 25, "Cousin ou cousine au 9e degré, à 15 générations d'écart")]
  [InlineData(1, 1, "Unsupported or wrong relationship: Type=Child, Sex=Unknown, G1, C1")]
  public void FR_UnknownSex_Cousin(int generation, int consanguinity, string expected)
  {
    SetFr();
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
  [InlineData(BiologicalSex.Female, "Cousine au deuxième degré")]
  [InlineData(BiologicalSex.Male, "Cousin au deuxième degré")]
  public void FR_Cousin_IsGendered(BiologicalSex sex, string expected)
  {
    SetFr();
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

  [Theory]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Male, "Beau-père")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Female, "Belle-mère")]
  [InlineData(RelationshipType.HusbandParent, BiologicalSex.Unknown, "Beau-père ou belle-mère")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Male, "Beau-père")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Female, "Belle-mère")]
  [InlineData(RelationshipType.WifeParent, BiologicalSex.Unknown, "Beau-père ou belle-mère")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Male, "Beau-père")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Female, "Belle-mère")]
  [InlineData(RelationshipType.SpouseParent, BiologicalSex.Unknown, "Beau-père ou belle-mère")]
  public void FR_InLawParent(RelationshipType type, BiologicalSex inLawSex, string expected)
  {
    SetFr();
    var actual = _formatter.ToString(
      type,
      inLawSex,
      Generation.Parent,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // French says "beau-père" for both a father-in-law and a stepfather. The bare word is left to the
  // in-law reading, which is the one an unqualified label carries in genealogical use, so the step
  // rows have to spell the qualifier out to stay distinguishable.
  [Theory]
  [InlineData(BiologicalSex.Male, "Beau-père (par remariage)")]
  [InlineData(BiologicalSex.Female, "Belle-mère (par remariage)")]
  [InlineData(BiologicalSex.Unknown, "Beau-père ou belle-mère (par remariage)")]
  public void FR_StepParent_StaysDistinctFromInLawParent(BiologicalSex sex, string expected)
  {
    SetFr();
    var actual = _formatter.ToString(
      RelationshipType.StepParent,
      sex,
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

  [Theory]
  [InlineData(BiologicalSex.Female, "Belle-sœur")]
  [InlineData(BiologicalSex.Male, "Beau-frère")]
  [InlineData(BiologicalSex.Unknown, "Beau-frère ou belle-sœur")]
  public void FR_SiblingSpouse(BiologicalSex spouseSex, string expected)
  {
    SetFr();
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
  [InlineData(BiologicalSex.Unknown, "Сводный брат или сводная сестра")]
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
  [InlineData(BiologicalSex.Unknown, "Stiefbruder oder Stiefschwester")]
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

  // "beau-frère" already means brother-in-law, so the French step-sibling rows qualify the plain
  // sibling term instead of reusing it.
  [Theory]
  [InlineData(BiologicalSex.Female, "Sœur par remariage")]
  [InlineData(BiologicalSex.Male, "Frère par remariage")]
  [InlineData(BiologicalSex.Unknown, "Frère par remariage ou sœur par remariage")]
  public void FR_StepSibling(BiologicalSex sex, string expected)
  {
    SetFr();
    var actual = _formatter.ToString(
      RelationshipType.StepSibling,
      sex,
      Generation.Zero,
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }

  // One template serves all three sexes, so the parent it names has to sit in a phrase rather than
  // in an adjective that would have to agree.
  [Theory]
  [InlineData(BiologicalSex.Female, "Demi-sœur du côté du père")]
  [InlineData(BiologicalSex.Male, "Demi-frère du côté du père")]
  [InlineData(BiologicalSex.Unknown, "Demi-frère du côté du père ou demi-sœur du côté du père")]
  public void FR_SiblingByFather(BiologicalSex sex, string expected)
  {
    SetFr();
    var actual = _formatter.ToString(
      RelationshipType.SiblingByFather,
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

  // French trails the adjective like Spanish but still prefixes the phrase, so both ends of the
  // label move independently.
  [Theory]
  [InlineData(2, "Grand-père adoptif")]
  [InlineData(4, "Arrière-arrière-grand-père adoptif")]
  public void FR_AdoptiveAncestor_KeepsTrailingAdjective(int generation, string expected)
  {
    SetFr();
    var actual = _formatter.ToString(
      RelationshipType.AdoptiveParent,
      BiologicalSex.Male,
      ToGeneration(generation),
      Consanguinity.Zero);

    Assert.Equal(expected, actual);
  }
}
