using FluentAssertions;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
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

  [Fact]
  public async Task Demo_CarriesPortraitsCreditingTheirSource()
  {
    // Six of the fourteen have a surviving likeness, and the rest deliberately have none: a tree where
    // everyone has a portrait would never show what the app does with a person who has no photo.
    await using var document = await ImportDemoAsync();

    var persons = await document.Persons.GetPersonsAsync(Token);
    var mainPhotos = await document.PersonData.GetMergedPhotoSetAsync(persons, DataCategory.PersonMainPhoto, Token);
    mainPhotos.Count(entry => entry.Value.Length != 0).Should().Be(6);

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var full = await document.PersonManager.GetPersonFullInfoAsync(byName["Charlotte Brontë"], Token);

    // Every OBJE carries a TITL crediting the work and its source, which is also what routes the photo
    // to the tagged category and its Content into a GedcomPhotoResidue envelope.
    full.MainPhoto!.Category.Should().Be(DataCategory.PersonMainPhotoTagged);
    var title = await GedcomPhotoResidue.ExtractTitleAsync(full.MainPhoto, Token);
    title.Should().Contain("Public domain, via Wikimedia Commons.");

    // Named rather than merely counted: Charlotte's two portraits both credit Wikimedia, so only the
    // artist tells whether _PRIM Y actually chose the profile photo or document order happened to.
    title.Should().Contain("Richmond");

    // The images ride in as base64 BLOBs split across CONT lines. A fold that dropped or reordered a
    // line still decodes to plausible bytes and the importer would then drop the photo without a word,
    // so both JPEG markers are asserted: the whole image has to survive, not just its opening.
    full.MainPhoto.MimeType.Should().Be("image/jpeg");
    var image = GedcomPhotoResidue.ExtractImageBytes(full.MainPhoto.Content);
    image.Take(2).Should().Equal((byte)0xFF, (byte)0xD8);
    image.TakeLast(2).Should().Equal((byte)0xFF, (byte)0xD9);

    // Charlotte is the one person carrying two portraits, so she is where _PRIM Y does any work.
    full.AdditionalPhotos.Should().ContainSingle();

    // Consumed OBJEs are pruned out of the residue. Were they not, the demo project would carry every
    // portrait a second time, as base64 text, in the blob the person's unmodeled tags live in.
    var residue = Encoding.UTF8.GetString(full.GedcomData!.Content);
    residue.Should().NotContain(GedcomTags.Blob);
  }

  [Fact]
  public async Task Demo_CarriesMarkdownBiographies()
  {
    // PersonPage renders a biography through MarkdownView, so the demo's are written as Markdown; what
    // this pins is that GEDCOM line folding hands back the shape Markdig needs to parse.
    await using var document = await ImportDemoAsync();

    var persons = await document.Persons.GetPersonsAsync(Token);
    var biographies = await document.PersonData.GetPersonDataSetAsync(persons, DataCategory.PersonBio, Token);
    biographies.Count(entry => entry.Value.Length != 0).Should().Be(10);

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var full = await document.PersonManager.GetPersonFullInfoAsync(byName["Patrick Brontë"], Token);
    var text = Encoding.UTF8.GetString(full.Biography!.Content);

    // An empty CONT contributes a newline and nothing else, so a paragraph break is written as two of
    // them. The blank line is load-bearing: MarkdownView reads a lone newline as a hard line break.
    text.Should().Contain("\n\nOrdained in 1806");

    // CONC continues a line rather than breaking it, so a paragraph longer than one GEDCOM line comes
    // back whole instead of splitting mid-sentence into two rendered lines.
    text.Should().Contain("he entered his name in the new spelling he kept");
  }
}
