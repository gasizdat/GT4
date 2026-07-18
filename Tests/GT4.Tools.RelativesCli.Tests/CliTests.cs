using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Tools.RelativesCli.Tests;

/// <summary>
/// Drives <see cref="Cli.RunAsync"/> end-to-end (argument parsing, GEDCOM import, SQLite project,
/// console formatting) against a three-person fixture: Robert Williams (I1) married to Mary Wilson
/// (I2), with child Joe Williams (I3).
/// </summary>
public sealed class CliTests : IDisposable
{
  private const string FixtureGedcom = """
    0 HEAD
    1 GEDC
    2 VERS 5.5.1
    2 FORM LINEAGE-LINKED
    1 CHAR UTF-8
    0 @I1@ INDI
    1 NAME Robert /Williams/
    1 SEX M
    1 BIRT
    2 DATE 2 Oct 1822
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
    2 DATE Dec 1859
    0 TRLR
    """;

  private static readonly CancellationToken Token = CancellationToken.None;

  private readonly string _root;
  private readonly string _gedcomPath;
  private readonly string _dbPath;
  private readonly StringWriter _stdout = new();
  private readonly StringWriter _stderr = new();
  private readonly TextWriter _originalOut = Console.Out;
  private readonly TextWriter _originalError = Console.Error;

  public CliTests()
  {
    _root = Path.Combine(Path.GetTempPath(), $"gt4_cli_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_root);
    _gedcomPath = Path.Combine(_root, "fixture.ged");
    _dbPath = Path.Combine(_root, "fixture.db");
    File.WriteAllText(_gedcomPath, FixtureGedcom);
    Console.SetOut(_stdout);
    Console.SetError(_stderr);
  }

  public void Dispose()
  {
    Console.SetOut(_originalOut);
    Console.SetError(_originalError);
    try { Directory.Delete(_root, true); } catch { /* best-effort temp cleanup */ }
  }

  [Fact]
  public async Task NoArgs_PrintsUsage_AndFails()
  {
    var exitCode = await Cli.RunAsync([], Token);

    exitCode.Should().Be(1);
    _stderr.ToString().Should().Contain("Usage:");
  }

  [Fact]
  public async Task UnknownMode_PrintsUsage_AndFails()
  {
    var exitCode = await Cli.RunAsync(["--frobnicate"], Token);

    exitCode.Should().Be(1);
    _stderr.ToString().Should().Contain("Usage:");
  }

  [Fact]
  public async Task MissingArgumentValue_Throws()
  {
    var run = () => Cli.RunAsync(["--db"], Token);

    await run.Should().ThrowAsync<ArgumentException>().WithMessage("Missing expected argument.");
  }

  [Fact]
  public async Task MissingCommand_PrintsUsage_AndFails()
  {
    var exitCode = await Cli.RunAsync(["--gedcom", _gedcomPath, "--out", _dbPath], Token);

    exitCode.Should().Be(1);
    _stderr.ToString().Should().Contain("Usage:");
  }

  [Fact]
  public async Task UnknownCommand_PrintsUsage_AndFails()
  {
    var exitCode = await Cli.RunAsync(["--gedcom", _gedcomPath, "--out", _dbPath, "explode"], Token);

    exitCode.Should().Be(1);
    _stderr.ToString().Should().Contain("Usage:");
  }

  [Fact]
  public async Task GedcomFind_MatchesCaseInsensitively_AndFormatsBirthDate()
  {
    var exitCode = await Cli.RunAsync(["--gedcom", _gedcomPath, "--out", _dbPath, "find", "robert"], Token);

    exitCode.Should().Be(0);
    var output = _stdout.ToString();
    output.Should().Contain($"Imported GEDCOM into: {_dbPath}");
    output.Should().Contain("Robert Williams").And.Contain("1822-10-02");
    output.Should().NotContain("Mary");
  }

  [Fact]
  public async Task DbRelatives_ListsSpouseAndChild()
  {
    var robertId = await ImportFixtureAndFindAsync("Robert");

    var exitCode = await Cli.RunAsync(["--db", _dbPath, "relatives", robertId], Token);

    exitCode.Should().Be(0);
    var output = _stdout.ToString();
    output.Should().Contain("Mary Wilson").And.Contain("Spouse");
    output.Should().Contain("Joe Williams").And.Contain("Child");
  }

  [Fact]
  public async Task Relatives_UnknownId_ReportsError()
  {
    await ImportFixtureAndFindAsync("Robert");

    var exitCode = await Cli.RunAsync(["--db", _dbPath, "relatives", "999"], Token);

    exitCode.Should().Be(0);
    _stderr.ToString().Should().Contain("No person with Id 999.");
  }

  [Fact]
  public async Task DbTree_WalksFromPersonsRelatives()
  {
    var robertId = await ImportFixtureAndFindAsync("Robert");

    var exitCode = await Cli.RunAsync(["--db", _dbPath, "tree", robertId], Token);

    exitCode.Should().Be(0);
    var output = _stdout.ToString();
    output.Should().Contain("Mary Wilson").And.Contain("Joe Williams");
    output.Should().NotContain("[LOOP]");
  }

  [Theory]
  [InlineData(DateStatus.WellKnown, "1822-10-02")]
  [InlineData(DateStatus.DayUnknown, "1822-10")]
  [InlineData(DateStatus.MonthUnknown, "~1822")]
  [InlineData(DateStatus.YearApproximate, "~1822")]
  [InlineData(DateStatus.Unknown, "?")]
  public void FormatDate_FollowsStatus(DateStatus status, string expected)
  {
    var date = Date.Create(1822, 10, 2, status);

    Cli.FormatDate(date).Should().Be(expected);
  }

  private async Task<string> ImportFixtureAndFindAsync(string query)
  {
    var exitCode = await Cli.RunAsync(["--gedcom", _gedcomPath, "--out", _dbPath, "find", query], Token);
    exitCode.Should().Be(0);

    var matchLine = _stdout.ToString()
      .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
      .Single(l => l.Contains(query));
    var id = matchLine.TrimStart().Split(' ')[0];

    _stdout.GetStringBuilder().Clear();
    return id;
  }
}
