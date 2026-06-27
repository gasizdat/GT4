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
  private readonly GedcomExporter _exporter = new();

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
    var path = Path.Combine(Path.GetTempPath(), $"gt4_gedcom_{Guid.NewGuid():N}.db");
    _paths.Add(path);
    return await ProjectDocument.CreateNewAsync(path, "gedcom", Token);
  }

  private async Task<ProjectDocument> ImportSampleAsync(string fileName)
  {
    var document = await NewDocumentAsync();
    using var reader = OpenSample(fileName);
    await _importer.ImportAsync(document, reader, Token);
    return document;
  }

  private async Task<string> ExportToTextAsync(ProjectDocument document)
  {
    var writer = new StringWriter();
    await _exporter.ExportAsync(document, writer, Token);
    return writer.ToString();
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
  public async Task Import_GroupsPersonsIntoFamiliesBySurname()
  {
    // The families page lists FamilyName groupings and the persons linked to each. The importer must build
    // them, or imported persons are invisible there even though the individuals and edges exist.
    await using var document = await ImportSampleAsync("family.ged");

    var families = await document.FamilyManager.GetFamiliesAsync(Token);
    families.Select(f => f.Value).Should().BeEquivalentTo("Williams", "Wilson");

    var williams = families.Single(f => f.Value == "Williams");
    var members = await document.PersonManager.GetPersonInfosByNameAsync(williams, selectMainPhoto: false, Token);
    members.Select(m => m.DisplayName).Should().BeEquivalentTo("Robert Eugene Williams", "Joe Williams", "Anna Williams");
  }

  [Fact]
  public async Task Import_SplitsMultiTokenGivenIntoFirstNameAndPatronymic()
  {
    // GT4 has no middle-name slot: a multi-token GIVN ("Robert Eugene") splits into a first name and a
    // patronymic, the only second-given slot, which is also where the exporter looks to rebuild the GIVN.
    await using var document = await ImportSampleAsync("family.ged");

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var robert = byName["Robert Eugene Williams"];
    var infos = await document.PersonManager.GetPersonInfosAsync([robert], selectMainPhoto: false, Token);
    var names = infos.Single().Names;
    names.Should().ContainSingle(n => (n.Type & NameType.FirstName) != 0 && n.Value == "Robert");
    names.Should().ContainSingle(n => (n.Type & NameType.Patronymic) != 0 && n.Value == "Eugene");

    // The split round-trips: export rejoins the first name and patronymic into the GIVN it came from.
    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 GIVN Robert Eugene");
  }

  [Fact]
  public async Task Import_SplitsEachExtraGivenTokenIntoItsOwnPatronymic()
  {
    // "Patrick Branwell Josef" -> first name Patrick plus two separate patronymics, not one joined blob.
    const string ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Patrick Branwell Josef /Stone/\n2 GIVN Patrick Branwell Josef\n2 SURN Stone\n1 SEX M\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var persons = await document.Persons.GetPersonsAsync(Token);
    var infos = await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, Token);
    var patronymics = infos.Single().Names.Where(n => (n.Type & NameType.Patronymic) != 0).Select(n => n.Value);
    patronymics.Should().Equal("Branwell", "Josef");

    // Both patronymics rejoin into the GIVN in order on export.
    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 GIVN Patrick Branwell Josef");
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
  public async Task UnmodeledRecords_PreservedInMetadataAndSurviveReExport()
  {
    await using var document = await ImportSampleAsync("sources.ged");

    // Each unmodeled top-level record is stashed verbatim, keyed by tag + xref. The two SOUR records prove
    // per-record keying: multiple records of the same tag coexist (the reason a single blob was rejected).
    foreach (var key in new[] { "gedcom.SUBM.@U1@", "gedcom.SUBN.@N1@", "gedcom.SOUR.@S1@", "gedcom.SOUR.@S2@", "gedcom.REPO.@R1@" })
    {
      (await document.Metadata.GetAsync<string>(key, Token)).Should().NotBeNull();
    }

    // The whole subtree is kept, so a cross-reference among passthrough records (SOUR -> REPO) survives.
    var source = await document.Metadata.GetAsync<string>("gedcom.SOUR.@S1@", Token);
    source.Should().Contain("0 @S1@ SOUR").And.Contain("Madison County").And.Contain("1 REPO @R1@");

    // The person is still imported normally.
    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    byName.Should().ContainSingle().Which.Key.Should().Be("Robert Eugene Williams");

    // Export re-emits the records (both sources), and a fresh re-import stashes them again unchanged.
    var text = await ExportToTextAsync(document);
    text.Should().Contain("0 @U1@ SUBM").And.Contain("0 @N1@ SUBN")
        .And.Contain("0 @S1@ SOUR").And.Contain("0 @S2@ SOUR").And.Contain("0 @R1@ REPO");

    // The source CITATIONS on the events are dropped: the page detail that only lived on the citation is gone.
    text.Should().NotContain("Sec. 2, p. 45");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);
    var repo = await reimported.Metadata.GetAsync<string>("gedcom.REPO.@R1@", Token);
    repo.Should().NotBeNull();
    repo.Should().Contain("Family History Library");
  }

  [Fact]
  public async Task UnmodeledIndividualSubTags_StoredVerbatimAndRestoredOnExport()
  {
    await using var document = await ImportSampleAsync("residue.ged");

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var person = byName.Should().ContainSingle().Which.Value;

    // The unmodeled INDI children are stashed as one verbatim residue blob linked to the person.
    var residue = await document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonGedcomTags, Token);
    residue.Should().ContainSingle();

    // Export re-emits them under the regenerated INDI, nesting and order intact, alongside the modeled BIRT.
    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 DATE 1 JAN 1850")          // owned BIRT, regenerated from the DB
        .And.Contain("1 OCCU Blacksmith").And.Contain("2 DATE FROM 1870 TO 1900")
        .And.Contain("1 RESI").And.Contain("2 PLAC London, England")
        .And.Contain("1 BURI").And.Contain("2 PLAC Highgate Cemetery")
        .And.Contain("1 EVEN").And.Contain("2 TYPE Census");

    // A fresh round-trip of the exported text preserves them again unchanged.
    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);
    var reexported = await ExportToTextAsync(reimported);
    reexported.Should().Contain("1 OCCU Blacksmith").And.Contain("1 EVEN").And.Contain("2 TYPE Census");
  }

  [Fact]
  public async Task UnmodeledIndividualSubTags_ProjectToFactsForDisplay()
  {
    await using var document = await ImportSampleAsync("residue.ged");

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    var person = byName.Should().ContainSingle().Which.Value;
    var residue = await document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonGedcomTags, Token);

    // The display projection keeps each residual child as a fact in document order, with its nested
    // sub-tags, so the UI can label and render them (OCCU -> DATE, RESI -> PLAC).
    var facts = await GedcomNarrative.ParseAsync(residue.Single(), Token);
    facts.Select(f => f.Tag).Should().Equal("OCCU", "RESI", "BURI", "EVEN");

    var occupation = facts[0];
    occupation.Value.Should().Be("Blacksmith");
    var occupationDate = occupation.Children.Should().ContainSingle().Which;
    occupationDate.Tag.Should().Be("DATE");
    occupationDate.Value.Should().Be("FROM 1870 TO 1900");

    facts[3].Children.Select(c => c.Tag).Should().Contain("TYPE");
  }

  [Fact]
  public async Task PersonGedcomTags_SurviveAPersonEdit()
  {
    await using var document = await ImportSampleAsync("residue.ged");

    // A UI edit round-trips the person through GetPersonFullInfo -> UpdatePerson. The GEDCOM tags are not
    // editable there, so they ride along on PersonFullInfo.GedcomData; without that they would be wiped by
    // UpdatePersonDataSet's delete-and-re-add.
    var persons = await document.Persons.GetPersonsAsync(Token);
    var full = await document.PersonManager.GetPersonFullInfoAsync(persons.Single(), Token);
    full.GedcomData.Should().NotBeNull();

    await document.PersonManager.UpdatePersonAsync(full with { DeathDate = Date.Create(1900, 1, 1, DateStatus.WellKnown) }, Token);

    var text = await ExportToTextAsync(document);
    text.Should().Contain("1 OCCU Blacksmith").And.Contain("1 EVEN").And.Contain("2 TYPE Census");
  }

  [Fact]
  public async Task EmbeddedObje_ImportsBlobAsMainPhoto()
  {
    var blob = Convert.ToBase64String(Encoding.UTF8.GetBytes("tiny-image"));
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      $"1 OBJE\n2 FORM jpeg\n2 _PRIM Y\n2 BLOB {blob}\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().NotBeNull();
    full.MainPhoto!.MimeType.Should().Be("image/jpeg");
    Encoding.UTF8.GetString(full.MainPhoto.Content).Should().Be("tiny-image");

    // A consumed photo is not also stored as opaque residue.
    full.GedcomData.Should().BeNull();
  }

  [Fact]
  public async Task MultipleEmbeddedPhotos_FirstBecomesMainWhenNonePrimary()
  {
    var first = Convert.ToBase64String(Encoding.UTF8.GetBytes("first"));
    var second = Convert.ToBase64String(Encoding.UTF8.GetBytes("second"));
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      $"1 OBJE\n2 FORM png\n2 BLOB {first}\n1 OBJE\n2 FORM png\n2 BLOB {second}\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    Encoding.UTF8.GetString(full.MainPhoto!.Content).Should().Be("first");
    Encoding.UTF8.GetString(full.AdditionalPhotos.Should().ContainSingle().Which.Content).Should().Be("second");
  }

  [Fact]
  public async Task FileReferenceObje_FallsBackToResidue()
  {
    // A third-party OBJE that points at an external FILE has no bytes GT4 can load, so it is not a photo;
    // it must survive verbatim through the residue passthrough rather than being dropped.
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      "1 OBJE\n2 FILE photo.jpg\n3 FORM jpeg\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().BeNull();
    full.AdditionalPhotos.Should().BeEmpty();
    full.GedcomData.Should().NotBeNull();

    var text = await ExportToTextAsync(document);
    text.Should().Contain("1 OBJE").And.Contain("2 FILE photo.jpg");
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
