using FluentAssertions;
using GT4.Tools.RelativesCli;
using Xunit;

namespace GT4.Tools.RelativesCli.Tests;

/// <summary>
/// Pins what the round-trip harness relies on the comparer to tolerate and what it must never wave through.
/// The fixtures are hand-written rather than exported, so each test isolates one transformation instead of
/// the dozen a real export applies at once.
/// </summary>
public sealed class GedcomComparerTests
{
  private static CancellationToken Token => TestContext.Current.CancellationToken;

  private const string Couple = """
    0 HEAD
    0 @I1@ INDI
    1 NAME Robert /Williams/
    1 SEX M
    1 BIRT
    2 DATE 2 OCT 1822
    1 FAMS @F1@
    0 @I2@ INDI
    1 NAME Mary /Wilson/
    1 SEX F
    1 FAMS @F1@
    0 @I3@ INDI
    1 NAME Joe /Williams/
    1 SEX M
    1 FAMC @F1@
    0 @F1@ FAM
    1 HUSB @I1@
    1 WIFE @I2@
    1 CHIL @I3@
    1 MARR
    2 DATE DEC 1859
    0 TRLR
    """;

  private static async Task<GedcomDifference[]> CompareAsync(string first, string second)
  {
    using var left = new StringReader(first);
    using var right = new StringReader(second);
    return await GedcomComparer.CompareAsync(left, right, Token);
  }

  [Fact]
  public async Task IdenticalFiles_HaveNoDifferences()
  {
    var differences = await CompareAsync(Couple, Couple);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task RenumberedXrefs_AreNotADifference()
  {
    var renumbered = Couple
      .Replace("@I1@", "@I77@")
      .Replace("@I2@", "@I78@")
      .Replace("@I3@", "@I79@")
      .Replace("@F1@", "@F9@");

    var differences = await CompareAsync(Couple, renumbered);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task ReorderedRecords_AreNotADifference()
  {
    var reordered = """
      0 HEAD
      0 @F1@ FAM
      1 HUSB @I1@
      1 WIFE @I2@
      1 CHIL @I3@
      1 MARR
      2 DATE DEC 1859
      0 @I3@ INDI
      1 NAME Joe /Williams/
      1 SEX M
      1 FAMC @F1@
      0 @I2@ INDI
      1 NAME Mary /Wilson/
      1 SEX F
      1 FAMS @F1@
      0 @I1@ INDI
      1 NAME Robert /Williams/
      1 SEX M
      1 BIRT
      2 DATE 2 OCT 1822
      1 FAMS @F1@
      0 TRLR
      """;

    var differences = await CompareAsync(Couple, reordered);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task NameRebuiltFromGivnAndSurn_IsNotADifference()
  {
    // What export does: the NAME value is regenerated from GIVN/SURN, dropping a suffix the source had
    // written into the value itself.
    var derived = Couple.Replace("1 NAME Robert /Williams/", """
      1 NAME Robert /Williams/ Jr.
      2 GIVN Robert
      2 SURN Williams
      """);

    var differences = await CompareAsync(derived, Couple);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task DateQualifierGedcomCannotExpress_CollapsesToApproximateYear()
  {
    var ranged = Couple.Replace("2 DATE 2 OCT 1822", "2 DATE BET 2 OCT 1822 AND 3 OCT 1823");
    var approximate = Couple.Replace("2 DATE 2 OCT 1822", "2 DATE ABT 1822");

    var differences = await CompareAsync(ranged, approximate);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task CoupleSplitAcrossTwoFamilies_MatchesOneMergedFamily()
  {
    // The source records the marriage and the child separately; GT4 regenerates a single FAM for the pair.
    var split = """
      0 HEAD
      0 @I1@ INDI
      1 NAME Robert /Williams/
      1 SEX M
      1 BIRT
      2 DATE 2 OCT 1822
      1 FAMS @F1@
      1 FAMS @F2@
      0 @I2@ INDI
      1 NAME Mary /Wilson/
      1 SEX F
      1 FAMS @F1@
      1 FAMS @F2@
      0 @I3@ INDI
      1 NAME Joe /Williams/
      1 SEX M
      1 FAMC @F2@
      0 @F1@ FAM
      1 HUSB @I1@
      1 WIFE @I2@
      1 MARR
      2 DATE DEC 1859
      0 @F2@ FAM
      1 HUSB @I1@
      1 WIFE @I2@
      1 CHIL @I3@
      0 TRLR
      """;

    var differences = await CompareAsync(split, Couple);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task DroppedIndividual_IsReported()
  {
    var withoutChild = Couple
      .Replace("""
        0 @I3@ INDI
        1 NAME Joe /Williams/
        1 SEX M
        1 FAMC @F1@

        """, string.Empty)
      .Replace("1 CHIL @I3@\r\n", string.Empty)
      .Replace("1 CHIL @I3@\n", string.Empty);

    var differences = await CompareAsync(Couple, withoutChild);

    differences.Should().ContainSingle()
      .Which.Kind.Should().Be(GedcomDifferenceKind.UnmatchedIndividual);
  }

  [Fact]
  public async Task ChangedBirthDate_IsReportedAsUnmatched()
  {
    var moved = Couple.Replace("2 DATE 2 OCT 1822", "2 DATE 2 OCT 1823");

    var differences = await CompareAsync(Couple, moved);

    differences.Should().HaveCount(2);
    differences.Should().OnlyContain(d => d.Kind == GedcomDifferenceKind.UnmatchedIndividual);
  }

  [Fact]
  public async Task ReParentedChild_IsReportedOnBothSides()
  {
    var stepFather = """
      0 HEAD
      0 @I1@ INDI
      1 NAME Robert /Williams/
      1 SEX M
      1 BIRT
      2 DATE 2 OCT 1822
      1 FAMS @F1@
      0 @I2@ INDI
      1 NAME Mary /Wilson/
      1 SEX F
      1 FAMS @F1@
      1 FAMS @F2@
      0 @I3@ INDI
      1 NAME Joe /Williams/
      1 SEX M
      1 FAMC @F2@
      0 @I4@ INDI
      1 NAME Silas /Brown/
      1 SEX M
      1 FAMS @F2@
      0 @F1@ FAM
      1 HUSB @I1@
      1 WIFE @I2@
      1 MARR
      2 DATE DEC 1859
      0 @F2@ FAM
      1 HUSB @I4@
      1 WIFE @I2@
      1 CHIL @I3@
      0 TRLR
      """;
    var original = Couple.Replace("""
      0 @F1@ FAM
      """, """
      0 @I4@ INDI
      1 NAME Silas /Brown/
      1 SEX M
      0 @F1@ FAM
      """);

    var differences = await CompareAsync(original, stepFather);

    differences.Should().Contain(d => d.Kind == GedcomDifferenceKind.MissingEdge);
    differences.Should().Contain(d => d.Kind == GedcomDifferenceKind.ExtraEdge);
  }

  [Fact]
  public async Task LostMarriageDate_IsReported()
  {
    var undated = Couple.Replace("""
      1 MARR
      2 DATE DEC 1859
      """, "1 MARR");

    var differences = await CompareAsync(Couple, undated);

    differences.Should().ContainSingle()
      .Which.Kind.Should().Be(GedcomDifferenceKind.MarriageDate);
  }

  [Fact]
  public async Task AdoptedChild_IsNotConfusedWithABirthChild()
  {
    var adopted = Couple.Replace("""
      1 FAMC @F1@
      """, """
      1 FAMC @F1@
      2 PEDI adopted
      """);

    var differences = await CompareAsync(Couple, adopted);

    differences.Should().Contain(d => d.Kind == GedcomDifferenceKind.MissingEdge);
    differences.Should().Contain(d => d.Kind == GedcomDifferenceKind.ExtraEdge);
  }

  /// <summary>
  /// Two people with no name, no sex and no dates are indistinguishable by content; only the relatives they
  /// hang off tell them apart. bourbon has 21 such individuals, and getting this wrong turns one real
  /// difference into a cascade of phantom edge differences.
  /// </summary>
  [Fact]
  public async Task NamelessIndividuals_AreMatchedByTheirRelatives()
  {
    const string nameless = """
      0 HEAD
      0 @I1@ INDI
      1 NAME Robert /Williams/
      1 SEX M
      1 FAMS @F1@
      0 @I2@ INDI
      1 FAMS @F1@
      0 @I3@ INDI
      1 NAME Mary /Wilson/
      1 SEX F
      1 FAMS @F2@
      0 @I4@ INDI
      1 FAMS @F2@
      0 @F1@ FAM
      1 HUSB @I1@
      1 WIFE @I2@
      0 @F2@ FAM
      1 HUSB @I3@
      1 WIFE @I4@
      0 TRLR
      """;
    // Same two nameless people, but the records (and so the xrefs) appear in the opposite order.
    var swapped = nameless
      .Replace("@I2@", "@IX@")
      .Replace("@I4@", "@I2@")
      .Replace("@IX@", "@I4@");

    var differences = await CompareAsync(nameless, swapped);

    differences.Should().BeEmpty();
  }
}
