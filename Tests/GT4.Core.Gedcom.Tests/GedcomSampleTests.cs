using FluentAssertions;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Reflection;
using System.Text;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

/// <summary>
/// Imports hand-authored GEDCOM 5.5.1 fixtures (embedded under <c>Samples/</c>). Unlike the round-trip
/// tests, these files are not produced by our exporter: they carry tags we deliberately ignore
/// (<c>PLAC</c>, <c>BURI</c>, <c>ADOP</c>, ...), real-world date qualifiers, and value-only names, so they
/// exercise the importer against external-looking input rather than our own output.
/// </summary>
public sealed class GedcomSampleTests : IAsyncLifetime
{
  private readonly List<string> _paths = [];
  private static CancellationToken Token => TestContext.Current.CancellationToken;
  private readonly GedcomImporter _importer = new();

  public ValueTask InitializeAsync() => ValueTask.CompletedTask;

  public ValueTask DisposeAsync()
  {
    foreach (var path in _paths)
    {
      foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
      {
        try { File.Delete(path + suffix); } catch { /* best-effort temp cleanup */ }
      }
    }
    return ValueTask.CompletedTask;
  }

  private async Task<ProjectDocument> ImportSampleAsync(string fileName)
  {
    var path = Path.Combine(Path.GetTempPath(), $"gt4_gedcom_{Guid.NewGuid():N}.db");
    _paths.Add(path);
    var document = await ProjectDocument.CreateNewAsync(path, "gedcom", Token);
    using var reader = OpenSample(fileName);
    await _importer.ImportAsync(document, reader, Token);
    return document;
  }

  private static StreamReader OpenSample(string fileName)
  {
    var assembly = Assembly.GetExecutingAssembly();
    var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".{fileName}", StringComparison.Ordinal));
    var stream = assembly.GetManifestResourceStream(name)!;
    return new StreamReader(stream, Encoding.UTF8);
  }

  [Fact]
  public async Task Family_ImportsPersonFieldsMarriageAndBothChildKinds()
  {
    await using var document = await ImportSampleAsync("family.ged");

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    byName.Should().HaveCount(4);

    var robert = byName["Robert Eugene Williams"];
    robert.BiologicalSex.Should().Be(BiologicalSex.Male);
    robert.BirthDate.Should().Be(Date.Create(1822, 10, 2, DateStatus.WellKnown));
    robert.DeathDate.Should().Be(Date.Create(1905, 4, 14, DateStatus.WellKnown));

    // "BEF 1828" has no GT4 equivalent, so it collapses to an approximate year.
    var mary = byName["Mary Ann Wilson"];
    mary.BiologicalSex.Should().Be(BiologicalSex.Female);
    mary.BirthDate.Should().Be(Date.Create(1828, null, null, DateStatus.YearApproximate));

    var (spouses, parentChild) = await GedcomTestGraph.ExtractAsync(document, Token);
    spouses.Should().ContainSingle().Which.Should().StartWith("Mary Ann Wilson+Robert Eugene Williams@");
    parentChild.Should().BeEquivalentTo(
    [
      "Joe Williams<-Robert Eugene Williams",
      "Joe Williams<-Mary Ann Wilson",
      "adopt:Anna Williams<-Robert Eugene Williams",
      "adopt:Anna Williams<-Mary Ann Wilson",
    ]);
  }

  [Fact]
  public async Task SameSexCouple_ImportsSpouseEdgeAndSharedChild()
  {
    await using var document = await ImportSampleAsync("samesex.ged");

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    byName.Should().HaveCount(3);

    var (spouses, parentChild) = await GedcomTestGraph.ExtractAsync(document, Token);
    spouses.Should().ContainSingle().Which.Should().StartWith("Alex Stone+Brian Stone@");
    parentChild.Should().BeEquivalentTo(
    [
      "Cody Stone<-Alex Stone",
      "Cody Stone<-Brian Stone",
    ]);
  }

  [Fact]
  public async Task Minimal_ImportsValueOnlyNameWithNoRelationships()
  {
    await using var document = await ImportSampleAsync("minimal.ged");

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var person = byName.Should().ContainSingle().Which;
    person.Key.Should().Be("Lone Wanderer");
    person.Value.BiologicalSex.Should().Be(BiologicalSex.Unknown);

    var (spouses, parentChild) = await GedcomTestGraph.ExtractAsync(document, Token);
    spouses.Should().BeEmpty();
    parentChild.Should().BeEmpty();
  }
}
