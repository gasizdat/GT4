using FluentAssertions;
using GT4.Core.Gedcom;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomReaderWriterTests
{
  private CancellationToken _Token => TestContext.Current.CancellationToken;

  private const string Sample =
    """
    0 HEAD
    1 CHAR UTF-8
    0 @I1@ INDI
    1 NAME John /Smith/
    2 GIVN John
    2 SURN Smith
    1 SEX M
    1 NOTE line one
    2 CONT line two
    2 CONC  continued
    0 TRLR
    """;

  [Fact]
  public async Task Read_ParsesRecordsXrefsAndNestingAsync()
  {
    var roots = await GedcomReader.ReadAsync(new StringReader(Sample), _Token);

    roots.Select(r => r.Tag).Should().Equal("HEAD", "INDI", "TRLR");

    var individual = roots[1];
    individual.Xref.Should().Be("@I1@");
    individual.Child("SEX")!.Value.Should().Be("M");

    var name = individual.Child("NAME")!;
    name.Value.Should().Be("John /Smith/");
    name.Child("GIVN")!.Value.Should().Be("John");
    name.Child("SURN")!.Value.Should().Be("Smith");
  }

  [Fact]
  public async Task Read_FoldsContAndConcIntoOwnerValueAsync()
  {
    var roots = await GedcomReader.ReadAsync(new StringReader(Sample), _Token);

    var note = roots[1].Child("NOTE")!;

    note.Value.Should().Be("line one\nline two continued");
  }

  [Fact]
  public async Task PointerValue_IsNotMistakenForRecordIdentifierAsync()
  {
    var roots = await GedcomReader.ReadAsync(new StringReader("0 @F1@ FAM\n1 HUSB @I1@\n"), _Token);

    var family = roots.Single();
    family.Xref.Should().Be("@F1@");
    var husband = family.Child("HUSB")!;
    husband.Xref.Should().BeNull();
    husband.Value.Should().Be("@I1@");
  }

  [Fact]
  public async Task Write_Then_Read_PreservesMultilineValueAsync()
  {
    var note = new GedcomNode { Tag = "NOTE", Value = "first paragraph\nsecond paragraph" };
    var record = new GedcomNode { Tag = "INDI", Xref = "@I1@" };
    record.Add(note);

    var roundTripped = await WriteThenReadAsync(record, _Token);

    roundTripped.Xref.Should().Be("@I1@");
    roundTripped.Child("NOTE")!.Value.Should().Be("first paragraph\nsecond paragraph");
  }

  [Fact]
  public async Task Write_SplitsLongValueAcrossConc_AndReadsBackIdenticalAsync()
  {
    var longText = new string('x', 640);
    var record = new GedcomNode { Tag = "NOTE", Value = longText };

    var roundTripped = await WriteThenReadAsync(record, _Token);

    roundTripped.Value.Should().Be(longText);
  }

  [Fact]
  public async Task Write_PreservesSpaceLandingOnAChunkBoundaryAsync()
  {
    // The writer splits long values at a fixed 200-char offset; here a space sits at index 199 so it ends
    // the first chunk. The reader must not strip it, or a re-joined value silently loses the space.
    var value = new string('a', 199) + " " + new string('b', 100);
    var record = new GedcomNode { Tag = "NOTE", Value = value };

    var roundTripped = await WriteThenReadAsync(record, _Token);

    roundTripped.Value.Should().Be(value);
  }

  private static async Task<GedcomNode> WriteThenReadAsync(GedcomNode record, CancellationToken token)
  {
    var writer = new StringWriter();
    GedcomWriter.Write(writer, record);
    var roots = await GedcomReader.ReadAsync(new StringReader(writer.ToString()), token);
    return roots.Single();
  }
}
