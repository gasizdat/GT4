using FluentAssertions;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Reflection;
using System.Text;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

/// <summary>
/// Importing into a populated project. The importer reuses existing names and folds an incoming
/// individual into an existing person when they share a first name, family name and birth date (where an
/// absent or unknown birth date matches another absent or unknown one); every ambiguous case stays a new
/// person so two distinct people are never collapsed into one. A matched person is gap-filled (missing
/// fields only) and relationship edges that already exist are not duplicated.
/// </summary>
public sealed class GedcomMergeImportTests : IAsyncLifetime
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

  private async Task<ProjectDocument> NewDocumentAsync()
  {
    var path = Path.Combine(Path.GetTempPath(), $"gt4_merge_{Guid.NewGuid():N}.db");
    _paths.Add(path);
    return await ProjectDocument.CreateNewAsync(path, "gedcom", Token);
  }

  private Task ImportAsync(ProjectDocument document, string ged) =>
    _importer.ImportAsync(document, new StringReader(ged), Token);

  private static string Indi(string xref, string given, string surname, int? birthYear = null, int? deathYear = null, string? note = null)
  {
    var sb = new StringBuilder();
    sb.Append($"0 {xref} INDI\n1 NAME {given} /{surname}/\n1 SEX M\n");
    if (birthYear is not null)
      sb.Append($"1 BIRT\n2 DATE 1 JAN {birthYear}\n");
    if (deathYear is not null)
      sb.Append($"1 DEAT\n2 DATE 1 JAN {deathYear}\n");
    if (note is not null)
      sb.Append($"1 NOTE {note}\n");
    return sb.ToString();
  }

  private static string Doc(params string[] records) =>
    "0 HEAD\n1 CHAR UTF-8\n" + string.Concat(records) + "0 TRLR\n";

  private static string Sample(string fileName)
  {
    var assembly = Assembly.GetExecutingAssembly();
    var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".{fileName}", StringComparison.Ordinal));
    using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!, Encoding.UTF8);
    return reader.ReadToEnd();
  }

  private async Task<PersonInfo[]> PersonInfosAsync(ProjectDocument document)
  {
    var persons = await document.Persons.GetPersonsAsync(Token);
    return await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, Token);
  }

  [Fact]
  public async Task MatchingIndividual_FoldsIntoExistingPersonAndWiresNewRelationships()
  {
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));

    // The second file re-states John (same name + birth date) and adds a new child under him.
    await ImportAsync(document, Doc(
      Indi("@I1@", "John", "Smith", birthYear: 1850),
      Indi("@I2@", "Tom", "Smith", birthYear: 1880),
      "0 @F1@ FAM\n1 HUSB @I1@\n1 CHIL @I2@\n"));

    var infos = await PersonInfosAsync(document);
    infos.Select(p => p.DisplayName).Should().BeEquivalentTo("John Smith", "Tom Smith");

    var john = infos.Single(p => p.DisplayName == "John Smith");
    var tom = infos.Single(p => p.DisplayName == "Tom Smith");
    var relatives = await document.Relatives.GetRelativesAsync(john, Token);
    relatives.Should().ContainSingle(r => r.Id == tom.Id && r.Type == RelationshipType.Child);
  }

  [Fact]
  public async Task DifferentBirthDate_ImportsAsNewPerson()
  {
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1860)));

    var infos = await PersonInfosAsync(document);
    infos.Should().HaveCount(2);
    infos.Select(p => p.BirthDate.Year).Should().BeEquivalentTo([1850, 1860]);
  }

  [Fact]
  public async Task UndatedIncoming_DoesNotFoldIntoDatedExistingPerson()
  {
    // An undated person matches only another undated person: it must not fold into a namesake who has a
    // known birth date, since the date makes them a more specific, possibly different, individual.
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith")));

    (await PersonInfosAsync(document)).Should().HaveCount(2);
  }

  [Fact]
  public async Task UndatedIncoming_FoldsIntoExistingUndatedPersonWhenNameIsUnique()
  {
    // Most imported people carry no birth date; re-importing the same file must still recognise them by
    // a name that is unique on both sides rather than duplicate them.
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "Arthur", "Nicholls")));
    await ImportAsync(document, Doc(Indi("@I1@", "Arthur", "Nicholls")));

    (await PersonInfosAsync(document)).Should().ContainSingle();
  }

  [Fact]
  public async Task AmbiguousUndatedMatch_ImportsAsNewPerson()
  {
    // Two existing undated namesakes are ambiguous, so a third undated namesake cannot be assigned to
    // either without guessing; it is added new instead.
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(
      Indi("@I1@", "Arthur", "Nicholls"),
      Indi("@I2@", "Arthur", "Nicholls")));
    (await PersonInfosAsync(document)).Should().HaveCount(2);

    await ImportAsync(document, Doc(Indi("@I3@", "Arthur", "Nicholls")));
    (await PersonInfosAsync(document)).Should().HaveCount(3);
  }

  [Fact]
  public async Task AmbiguousExistingMatch_ImportsAsNewPerson()
  {
    // Two existing people share the identity, so a third with the same identity cannot be assigned to
    // either one without guessing; it is added new instead.
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(
      Indi("@I1@", "John", "Smith", birthYear: 1850),
      Indi("@I2@", "John", "Smith", birthYear: 1850)));
    (await PersonInfosAsync(document)).Should().HaveCount(2);

    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));
    (await PersonInfosAsync(document)).Should().HaveCount(3);
  }

  [Fact]
  public async Task ExistingFamilyName_IsReusedWithoutUniqueViolation()
  {
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));

    // A different person sharing the surname must not collide on the UNIQUE(Value, Type, ParentId) index.
    await ImportAsync(document, Doc(Indi("@I2@", "Mark", "Smith", birthYear: 1860)));

    (await PersonInfosAsync(document)).Should().HaveCount(2);
    var families = await document.FamilyManager.GetFamiliesAsync(Token);
    families.Where(f => f.Value == "Smith").Should().ContainSingle();
  }

  [Fact]
  public async Task ReimportingSameFile_DoesNotDuplicatePersonsOrEdges()
  {
    var file = Doc(
      Indi("@I1@", "John", "Smith", birthYear: 1850),
      Indi("@I2@", "Tom", "Smith", birthYear: 1880),
      "0 @F1@ FAM\n1 HUSB @I1@\n1 CHIL @I2@\n");

    await using var document = await NewDocumentAsync();
    await ImportAsync(document, file);
    // The child edge stores a null date; SQLite treats null primary-key parts as distinct, so without
    // explicit dedup this second import would silently insert a duplicate edge.
    await ImportAsync(document, file);

    var infos = await PersonInfosAsync(document);
    infos.Should().HaveCount(2);

    var john = infos.Single(p => p.DisplayName == "John Smith");
    var tom = infos.Single(p => p.DisplayName == "Tom Smith");
    var relatives = await document.Relatives.GetRelativesAsync(john, Token);
    relatives.Where(r => r.Id == tom.Id && r.Type == RelationshipType.Child).Should().ContainSingle();
  }

  [Fact]
  public async Task Matching_GapFillsMissingDeathDateAndBiography()
  {
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850, deathYear: 1910, note: "A blacksmith.")));

    var infos = await PersonInfosAsync(document);
    infos.Should().HaveCount(1);

    var full = await document.PersonManager.GetPersonFullInfoAsync(infos.Single(), Token);
    full.DeathDate.Should().Be(Date.Create(1910, 1, 1, DateStatus.WellKnown));
    full.Biography.Should().NotBeNull();
    Encoding.UTF8.GetString(full.Biography!.Content).Should().Be("A blacksmith.");
  }

  [Fact]
  public async Task Matching_DoesNotOverwritePopulatedDeathDateOrBiography()
  {
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850, deathYear: 1900, note: "First.")));
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850, deathYear: 1910, note: "Second.")));

    var infos = await PersonInfosAsync(document);
    infos.Should().HaveCount(1);

    var full = await document.PersonManager.GetPersonFullInfoAsync(infos.Single(), Token);
    full.DeathDate.Should().Be(Date.Create(1900, 1, 1, DateStatus.WellKnown));
    Encoding.UTF8.GetString(full.Biography!.Content).Should().Be("First.");
  }

  [Fact]
  public async Task Matching_GapFillsMissingPhotoAndResidue()
  {
    await using var document = await NewDocumentAsync();
    await ImportAsync(document, Doc(Indi("@I1@", "John", "Smith", birthYear: 1850)));

    // The re-stated person carries an embedded photo and an unmodeled OCCU tag the first one lacked.
    var blob = Convert.ToBase64String(Encoding.UTF8.GetBytes("tiny-image"));
    var enriched =
      "0 @I1@ INDI\n1 NAME John /Smith/\n1 SEX M\n1 BIRT\n2 DATE 1 JAN 1850\n" +
      $"1 OCCU Blacksmith\n1 OBJE\n2 FORM jpeg\n2 _PRIM Y\n2 BLOB {blob}\n";
    await ImportAsync(document, Doc(enriched));

    var infos = await PersonInfosAsync(document);
    infos.Should().HaveCount(1);

    var full = await document.PersonManager.GetPersonFullInfoAsync(infos.Single(), Token);
    full.MainPhoto.Should().NotBeNull();
    Encoding.UTF8.GetString(full.MainPhoto!.Content).Should().Be("tiny-image");
    full.GedcomData.Should().NotBeNull();
    Encoding.UTF8.GetString(full.GedcomData!.Content).Should().Contain("OCCU Blacksmith");
  }

  [Fact]
  public async Task ReimportingRealFile_KeepsEveryPersonDistinct()
  {
    // The real Brontë file mixes precise, year-only, month-year and "abt" birth dates, two people with no
    // birth date at all, and namesakes distinguished only by date (Patrick 1777 vs Patrick Branwell 1817).
    // Re-importing it must fold every individual back onto itself and add nothing, which only holds if each
    // identity round-trips through the database exactly as it was first stored.
    var ged = Sample("bronte.ged");
    await using var document = await NewDocumentAsync();

    await ImportAsync(document, ged);
    (await PersonInfosAsync(document)).Should().HaveCount(14);

    await ImportAsync(document, ged);
    (await PersonInfosAsync(document)).Should().HaveCount(14);
  }

  [Fact]
  public async Task ReimportingMarriedCouple_DoesNotDuplicateSpouseEdge()
  {
    var file = Doc(
      Indi("@I1@", "John", "Smith", birthYear: 1850),
      "0 @I2@ INDI\n1 NAME Jane /Smith/\n1 SEX F\n1 BIRT\n2 DATE 1 JAN 1855\n",
      "0 @F1@ FAM\n1 HUSB @I1@\n1 WIFE @I2@\n1 MARR\n2 DATE 5 MAY 1875\n");

    await using var document = await NewDocumentAsync();
    await ImportAsync(document, file);
    await ImportAsync(document, file);

    var infos = await PersonInfosAsync(document);
    infos.Should().HaveCount(2);

    var john = infos.Single(p => p.DisplayName == "John Smith");
    var jane = infos.Single(p => p.DisplayName == "Jane Smith");
    var relatives = await document.Relatives.GetRelativesAsync(john, Token);
    relatives.Where(r => r.Id == jane.Id && r.Type == RelationshipType.Spouse).Should().ContainSingle();
  }
}
