using FluentAssertions;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using System.Reflection;
using System.Text;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

/// <summary>
/// Imports the demo genealogy the app ships as a raw asset (linked in from <c>UI\App\Resources\Raw</c>,
/// not a fixture of this project). ProjectListPage offers it on an empty project list, so a typo in the
/// file would surface as a broken first launch; this is what keeps it a tree worth landing in.
/// </summary>
public sealed class DemoGedcomTests : IAsyncLifetime
{
  private static CancellationToken Token => TestContext.Current.CancellationToken;
  private readonly GedcomImporter _importer = new(new FileGedcomMediaReader());
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"gt4_demo_{Guid.NewGuid():N}.db");

  public ValueTask InitializeAsync() => ValueTask.CompletedTask;

  public ValueTask DisposeAsync()
  {
    foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
    {
      try { File.Delete(_path + suffix); } catch { /* best-effort temp cleanup */ }
    }
    return ValueTask.CompletedTask;
  }

  private async Task<ProjectDocument> ImportDemoAsync()
  {
    var document = await ProjectDocument.CreateNewAsync(_path, "demo", Token);
    var assembly = Assembly.GetExecutingAssembly();
    var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".demo.ged", StringComparison.Ordinal));
    using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!, Encoding.UTF8);
    await _importer.ImportAsync(document, reader, Token);
    return document;
  }

  [Fact]
  public async Task Demo_ImportsEveryPersonAndFamilyName()
  {
    await using var document = await ImportDemoAsync();

    var persons = await document.Persons.GetPersonsAsync(Token);
    persons.Should().HaveCount(14);

    var families = await document.FamilyManager.GetFamiliesAsync(Token);
    families.Select(family => family.Value).Should().BeEquivalentTo(
      "Branwell", "Brontë", "Brunty", "Carne", "McClory", "Nicholls");
  }

  [Fact]
  public async Task Demo_LinksThreeGenerationsAroundTheSisters()
  {
    // The family tree, statistics and kinship screens are the point of the demo, and all three need
    // edges rather than persons: Charlotte carries a marriage, both parents and five siblings.
    await using var document = await ImportDemoAsync();

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var relatives = await document.Relatives.GetRelativesAsync(byName["Charlotte Brontë"], Token);

    relatives.Should().ContainSingle(relative => relative.Type == RelationshipType.Spouse);
    relatives.Count(relative => relative.Type == RelationshipType.Parent).Should().Be(2);

    var grandparents = await document.Relatives.GetRelativesAsync(byName["Patrick Brontë"], Token);
    grandparents.Count(relative => relative.Type == RelationshipType.Child).Should().Be(6);
    grandparents.Count(relative => relative.Type == RelationshipType.Parent).Should().Be(2);
  }
}
