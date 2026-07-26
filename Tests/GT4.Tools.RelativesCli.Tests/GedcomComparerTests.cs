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

  [Fact]
  public async Task LostResidueTag_IsReported()
  {
    // OCCU is a tag GT4 models nothing of but promises to keep verbatim.
    var withOccupation = Couple.Replace("""
      1 NAME Robert /Williams/
      """, """
      1 NAME Robert /Williams/
      1 OCCU Ambassador
      """);

    var differences = await CompareAsync(withOccupation, Couple);

    differences.Should().ContainSingle()
      .Which.Should().Match<GedcomDifference>(d => d.Kind == GedcomDifferenceKind.Tag && d.Detail.Contains("OCCU"));
  }

  [Fact]
  public async Task NestedResidueChange_IsReported()
  {
    var moved = Couple.Replace("""
      1 BIRT
      2 DATE 2 OCT 1822
      """, """
      1 BIRT
      2 DATE 2 OCT 1822
      2 PLAC Elsewhere
      """);

    var differences = await CompareAsync(Couple, moved);

    differences.Should().Contain(d => d.Kind == GedcomDifferenceKind.Tag);
  }

  [Fact]
  public async Task LostFamilyResidueTag_IsReported()
  {
    var withPlace = Couple.Replace("""
      1 MARR
      2 DATE DEC 1859
      """, """
      1 MARR
      2 DATE DEC 1859
      2 PLAC Boston
      """);

    var differences = await CompareAsync(withPlace, Couple);

    differences.Should().Contain(d => d.Kind == GedcomDifferenceKind.Tag && d.Detail.Contains("PLAC"));
  }

  [Fact]
  public async Task ContentOfAFamilyNoEdgeSurvives_IsNotReported()
  {
    // A couple asserted by HUSB alone is regenerated by nothing, so the whole record is gone -- reported once
    // as a missing edge where there is one, never again per tag it took with it (issue #186).
    var lonelyFamily = Couple.Replace("0 TRLR", "0 @F2@ FAM\r\n1 HUSB @I1@\r\n1 CHAN\r\n2 DATE 1 JAN 2000\r\n0 TRLR");

    var differences = await CompareAsync(lonelyFamily, Couple);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task SameResidueOnBothOfACouplesFamilies_MatchesOneMergedFamily()
  {
    // GT4 keeps a couple's residue as a set under their one key, so the second record's CHAN folds into the
    // first and the merged family states it once.
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
      1 CHAN
      2 DATE 1 JAN 2000
      0 @F2@ FAM
      1 HUSB @I1@
      1 WIFE @I2@
      1 CHIL @I3@
      1 CHAN
      2 DATE 1 JAN 2000
      0 TRLR
      """;
    var merged = Couple.Replace("""
      1 MARR
      2 DATE DEC 1859
      """, """
      1 MARR
      2 DATE DEC 1859
      1 CHAN
      2 DATE 1 JAN 2000
      """);

    var differences = await CompareAsync(split, merged);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task MarriageDateGedcomStatesButGt4CannotHold_IsReportedRatherThanCollapsedToEqual()
  {
    var republican = Couple.Replace("2 DATE DEC 1859", "2 DATE @#DFRENCH R@ 25 VEND 2");
    var undated = Couple
      .Replace("2 DATE DEC 1859\r\n", string.Empty)
      .Replace("2 DATE DEC 1859\n", string.Empty);

    var differences = await CompareAsync(republican, undated);

    differences.Should().ContainSingle()
      .Which.Kind.Should().Be(GedcomDifferenceKind.NotRepresentable);
  }

  [Fact]
  public async Task NotePointerAndTheTextItPointsAt_AreTheSameNote()
  {
    var pointer = Couple
      .Replace("0 TRLR", "0 @N1@ NOTE Kept a general store.\r\n0 TRLR")
      .Replace("1 NAME Robert /Williams/", "1 NAME Robert /Williams/\r\n1 NOTE @N1@");
    var inline = Couple.Replace("1 NAME Robert /Williams/", "1 NAME Robert /Williams/\r\n1 NOTE Kept a general store.");

    var differences = await CompareAsync(pointer, inline);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task EmptyEventNode_IsNotADifference()
  {
    // "1 BIRT" with nothing under it asserts nothing and does not survive import; bourbon has 85 of them.
    var bare = Couple.Replace("1 NAME Mary /Wilson/", "1 NAME Mary /Wilson/\r\n1 BIRT");

    var differences = await CompareAsync(bare, Couple);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task DateGedcomStatesButGt4CannotHold_IsReportedRatherThanCollapsedToEqual()
  {
    // Both sides would canonicalize to nothing, which is exactly how silent loss gets certified as fidelity.
    var republican = Couple.Replace("1 SEX F", """
      1 SEX F
      1 DEAT
      2 DATE @#DFRENCH R@ 25 VEND 2
      """);

    var differences = await CompareAsync(republican, Couple);

    differences.Should().ContainSingle()
      .Which.Kind.Should().Be(GedcomDifferenceKind.NotRepresentable);
  }

  [Fact]
  public async Task MultiFileMedia_MatchesTheSingleFileMediaItIsSplitInto()
  {
    var combined = Couple.Replace("""
      1 NAME Robert /Williams/
      """, """
      1 NAME Robert /Williams/
      1 OBJE
      2 FILE photos/first.jpg
      2 FILE photos/second.jpg
      2 TITL Wedding
      """);
    var split = Couple.Replace("""
      1 NAME Robert /Williams/
      """, """
      1 NAME Robert /Williams/
      1 OBJE
      2 FILE photos/first.jpg
      2 TITL Wedding
      1 OBJE
      2 FILE photos/second.jpg
      2 TITL Wedding
      """);

    var differences = await CompareAsync(combined, split);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task EventNestedMedia_CountsAsThePersonsMedia()
  {
    var nested = Couple.Replace("""
      1 BIRT
      2 DATE 2 OCT 1822
      """, """
      1 BIRT
      2 DATE 2 OCT 1822
      2 OBJE
      3 TITL Baptism record
      """);
    var surfaced = Couple.Replace("""
      1 NAME Robert /Williams/
      """, """
      1 NAME Robert /Williams/
      1 OBJE
      2 TITL Baptism record
      """);

    var differences = await CompareAsync(nested, surfaced);

    differences.Should().BeEmpty();
  }

  [Fact]
  public async Task LostMedia_IsReported()
  {
    var withPhoto = Couple.Replace("""
      1 NAME Robert /Williams/
      """, """
      1 NAME Robert /Williams/
      1 OBJE
      2 TITL Portrait
      """);

    var differences = await CompareAsync(withPhoto, Couple);

    differences.Should().ContainSingle()
      .Which.Should().Match<GedcomDifference>(d => d.Kind == GedcomDifferenceKind.Media && d.Detail.Contains("Portrait"));
  }

  [Fact]
  public async Task LostPassthroughRecord_IsReported()
  {
    var withSource = Couple.Replace("0 TRLR", """
      0 @S1@ SOUR
      1 TITL Parish register
      0 TRLR
      """);

    var differences = await CompareAsync(withSource, Couple);

    differences.Should().ContainSingle()
      .Which.Kind.Should().Be(GedcomDifferenceKind.Record);
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
