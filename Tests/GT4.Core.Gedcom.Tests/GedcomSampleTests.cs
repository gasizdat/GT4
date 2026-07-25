using FluentAssertions;
using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Reflection;
using System.Text;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

/// <summary>
/// Imports hand-authored GEDCOM 5.5.1 fixtures (embedded under <c>Samples/</c>). Unlike the round-trip
/// tests, these files are not produced by our exporter: they carry tags GT4 does not model
/// (<c>PLAC</c>, <c>BURI</c>, <c>ADOP</c>, ...), real-world date qualifiers, and value-only names, so they
/// exercise the importer against external-looking input rather than our own output.
/// </summary>
public sealed class GedcomSampleTests : IAsyncLifetime
{
  private readonly List<string> _paths = [];
  private static CancellationToken Token => TestContext.Current.CancellationToken;
  private readonly GedcomImporter _importer = new(new FileGedcomMediaReader());
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

  // The attachment whose preserved FILE residual matches the given path -- lets a test pin one file when
  // several attachments (a split multi-file OBJE, a couple's shared media) coexist on one person.
  private static async Task<Data> AttachmentByFileAsync(Data[] attachments, string fileName)
  {
    foreach (var attachment in attachments)
    {
      if (await GedcomPhotoResidue.ExtractFileNameAsync(attachment, Token) == fileName)
        return attachment;
    }
    throw new InvalidOperationException($"No attachment references {fileName}");
  }

  // ExtractTitleAsync only serves photo captions, so an attachment's preserved TITL is read off the
  // envelope's residual directly.
  private static async Task<string?> AttachmentTitleAsync(Data attachment)
  {
    var (_, residual) = await GedcomPhotoResidue.DecodeAsync(attachment.Content, Token);
    return residual.ChildValue(GedcomTags.Title);
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

    // The event-level source citations are preserved as residue and merged back under the events, so the
    // BIRT's PAGE detail survives and still points at the re-emitted @S1@ record.
    text.Should().Contain("2 SOUR @S1@").And.Contain("3 PAGE Sec. 2, p. 45");

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
  public async Task OwnedTagSubDetails_PreservedAsResidueAndMergedOnExport()
  {
    // GT4 reads only DATE from BIRT and GIVN/SURN from NAME. The other sub-tags (PLAC -> MAP, a source
    // citation, a NICK) are unmodeled and must not be lost: they are stored as residue under a residual copy
    // of the owned tag, then merged back under the regenerated tag on export with no duplicate DATE or BIRT.
    const string ged =
      "0 HEAD\n1 CHAR UTF-8\n" +
      "0 @I1@ INDI\n1 NAME Louis /Capet/\n2 GIVN Louis\n2 SURN Capet\n2 NICK Sun King\n1 SEX M\n" +
      "1 BIRT\n2 DATE 5 SEP 1638\n2 PLAC Saint-Germain-en-Laye\n3 MAP\n4 LATI N48.8979\n4 LONG E2.09592\n2 SOUR Acte\n" +
      "0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var persons = await document.Persons.GetPersonsAsync(Token);
    var person = persons.Single();

    // Only the unmodeled sub-tags become residue, under a residual BIRT/NAME; the DATE/GIVN/SURN ride in the
    // GT4 model instead, so they are absent from the blob.
    var residue = await document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonGedcomTags, Token);
    var blob = Encoding.UTF8.GetString(residue.Single().Content);
    blob.Should().Contain("0 NAME").And.Contain("1 NICK Sun King")
        .And.Contain("0 BIRT").And.Contain("1 PLAC Saint-Germain-en-Laye")
        .And.Contain("2 MAP").And.Contain("3 LATI N48.8979").And.Contain("1 SOUR Acte");
    blob.Should().NotContain("DATE").And.NotContain("GIVN").And.NotContain("SURN");

    // Export merges them back under the single regenerated BIRT and NAME (no duplicate event).
    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 DATE 5 SEP 1638").And.Contain("2 PLAC Saint-Germain-en-Laye")
        .And.Contain("3 MAP").And.Contain("4 LATI N48.8979").And.Contain("2 SOUR Acte")
        .And.Contain("2 NICK Sun King");
    var births = text.Split('\n').Count(line => line.StartsWith("1 BIRT", StringComparison.Ordinal));
    births.Should().Be(1);
  }

  [Fact]
  public async Task CalendarEscapeDate_KeptAsResidueAndReEmittedVerbatim()
  {
    // A date in a non-Gregorian calendar is unparseable for GT4, so it belongs to the owned tag's residue.
    // The event itself is still modeled (a DEAT means "known dead"), so the residual DATE has to merge back
    // under that one regenerated event rather than come out as a second, bare one.
    const string ged =
      "0 HEAD\n1 CHAR UTF-8\n" +
      "0 @I1@ INDI\n1 NAME Louis /Capet/\n1 SEX M\n" +
      "1 BIRT\n2 DATE @#DJULIAN@ 23 AUG 1754\n" +
      "1 DEAT\n2 DATE @#DFRENCH R@ 2 PLUV 1\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    person.DeathDate!.Value.Status.Should().Be(DateStatus.Unknown);

    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 DATE @#DJULIAN@ 23 AUG 1754").And.Contain("2 DATE @#DFRENCH R@ 2 PLUV 1");
    var lines = text.Split('\n');
    lines.Count(line => line.StartsWith("1 BIRT", StringComparison.Ordinal)).Should().Be(1);
    lines.Count(line => line.StartsWith("1 DEAT", StringComparison.Ordinal)).Should().Be(1);

    // A second hop keeps them: the residue is rebuilt from the export, not just carried once.
    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);
    var reexported = await ExportToTextAsync(reimported);
    reexported.Should().Be(text);
  }

  [Fact]
  public async Task EventAssertion_KeptAsResidueAndReEmittedVerbatim()
  {
    // "1 DEAT Y" asserts the event happened with nothing else known -- a statement GT4 has no field for, so
    // the value belongs to the owned tag's residue. Export merges it back onto the one regenerated event:
    // GT4 already emits a bare "1 DEAT" for a dateless death, so a standalone replay would double the tag.
    const string ged =
      "0 HEAD\n1 CHAR UTF-8\n" +
      "0 @I1@ INDI\n1 NAME Louis /Capet/\n1 SEX M\n" +
      "1 BIRT Y\n2 TYPE Avec les cités de Lamballe\n" +
      "1 DEAT Y\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    person.DeathDate!.Value.Status.Should().Be(DateStatus.Unknown);

    var residue = await document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonGedcomTags, Token);
    var blob = Encoding.UTF8.GetString(residue.Single().Content);
    blob.Should().Contain("BIRT Y").And.Contain("TYPE Avec les cités de Lamballe").And.Contain("DEAT Y");

    var text = await ExportToTextAsync(document);
    text.Should().Contain("1 BIRT Y").And.Contain("2 TYPE Avec les cités de Lamballe").And.Contain("1 DEAT Y");
    var lines = text.Split('\n');
    lines.Count(line => line.StartsWith("1 BIRT", StringComparison.Ordinal)).Should().Be(1);
    lines.Count(line => line.StartsWith("1 DEAT", StringComparison.Ordinal)).Should().Be(1);

    // A second hop keeps them: the residue is rebuilt from the export, not just carried once.
    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);
    var reexported = await ExportToTextAsync(reimported);
    reexported.Should().Be(text);
  }

  [Fact]
  public async Task RepeatedOwnedTag_SecondNameAndNoteSurviveRoundTrip()
  {
    // GT4 consumes only the first NAME (identity) and first NOTE (biography); a second of either has no slot
    // in the model. It must be preserved whole as residue and re-emitted as its own standalone tag on export,
    // not merged into the first — which would silently drop its value.
    const string ged =
      "0 HEAD\n1 CHAR UTF-8\n" +
      "0 @I1@ INDI\n1 NAME John /Smith/\n1 NAME Johnny /Smith/\n1 SEX M\n" +
      "1 NOTE A blacksmith.\n1 NOTE Also a poet.\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();

    var residue = await document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonGedcomTags, Token);
    var blob = Encoding.UTF8.GetString(residue.Single().Content);
    blob.Should().Contain("NAME Johnny /Smith/").And.Contain("NOTE Also a poet.");

    var text = await ExportToTextAsync(document);
    text.Should().Contain("1 NAME John /Smith/").And.Contain("1 NAME Johnny /Smith/")
        .And.Contain("1 NOTE A blacksmith.").And.Contain("1 NOTE Also a poet.");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);
    var reexported = await ExportToTextAsync(reimported);
    reexported.Should().Contain("1 NAME Johnny /Smith/").And.Contain("1 NOTE Also a poet.");
  }

  [Fact]
  public async Task NotePointer_ResolvedToTextWhereverItSits()
  {
    // GT4 keeps no top-level NOTE record, so a pointer replayed out of residue would reference a record the
    // export does not contain. Each one is resolved to the record's text at import instead, whether the
    // pointer is consumed as a biography or preserved verbatim under a residual NAME or an unmodeled TITL.
    const string ged =
      "0 HEAD\n1 CHAR UTF-8\n" +
      "0 @I1@ INDI\n1 NAME Henri /Capet/\n2 NOTE @N1@\n1 SEX M\n1 NOTE @N2@\n" +
      "1 TITL Roi de France\n2 NOTE @N3@\n1 OCCU Roi\n2 NOTE @N9@\n" +
      "0 @I2@ INDI\n1 NAME Louis /Capet/\n1 SEX M\n1 NOTE @N2@\n" +
      "0 @N1@ NOTE Maison de Bourbon\n0 @N2@ NOTE Assassine par Francois Ravaillac\n" +
      "0 @N3@ NOTE Pretendant a la couronne\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    foreach (var person in byName.Values)
    {
      var biography = await document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonBio, Token);
      Encoding.UTF8.GetString(biography.Single().Content).Should().Be("Assassine par Francois Ravaillac");
    }

    var residue = await document.PersonData.GetPersonDataSetAsync(byName["Henri Capet"], DataCategory.PersonGedcomTags, Token);
    var blob = Encoding.UTF8.GetString(residue.Single().Content);
    // @N9@ answers to no record, so the pointer itself is all there is to keep.
    blob.Should().Contain("NOTE Maison de Bourbon").And.Contain("NOTE Pretendant a la couronne")
        .And.Contain("NOTE @N9@");

    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 NOTE Maison de Bourbon").And.Contain("1 NOTE Assassine par Francois Ravaillac")
        .And.Contain("2 NOTE Pretendant a la couronne").And.Contain("2 NOTE @N9@");
    text.Should().NotContain("NOTE @N1@").And.NotContain("NOTE @N2@").And.NotContain("NOTE @N3@");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);
    var reexported = await ExportToTextAsync(reimported);
    reexported.Should().Be(text);
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
  public async Task EmbeddedObje_NonImageFormImportsBlobAsAttachmentOnly()
  {
    // An embedded BLOB with an explicit non-image FORM (a PDF, base64'd inline) must be claimed by the
    // attachment pass alone -- never also picked up as a (broken) photo.
    var blob = Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF-tiny"));
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      $"1 OBJE\n2 FORM pdf\n2 FILE scan.pdf\n2 BLOB {blob}\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().BeNull();
    full.AdditionalPhotos.Should().BeEmpty();
    full.GedcomData.Should().BeNull();

    var attachment = full.Attachments.Should().ContainSingle().Which;
    attachment.MimeType.Should().Be("application/pdf");
    Encoding.UTF8.GetString(GedcomPhotoResidue.ExtractImageBytes(attachment.Content)).Should().Be("%PDF-tiny");
    (await GedcomPhotoResidue.ExtractFileNameAsync(attachment, Token)).Should().Be("scan.pdf");
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
  public async Task NestedObjeUnderEventSour_IsDiscoveredAsAttachmentAndNotDuplicatedOnExport()
  {
    // Mirrors issue #148: a certificate scan nested three levels deep, under BIRT -> SOUR, rather than a
    // direct INDI child -- previously invisible to SelectAttachments and only ever surfacing as opaque residue.
    var blob = Convert.ToBase64String(Encoding.UTF8.GetBytes("baptism-cert"));
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Louis /Bourbon/\n1 SEX M\n" +
      "1 BIRT\n2 DATE 5 SEP 1638\n2 SOUR @S1@\n3 DATA\n4 TEXT Parish register\n3 OBJE\n4 FORM pdf\n" +
      $"4 BLOB {blob}\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);

    var attachment = full.Attachments.Should().ContainSingle().Which;
    Encoding.UTF8.GetString(GedcomPhotoResidue.ExtractImageBytes(attachment.Content)).Should().Be("baptism-cert");

    // The BIRT's other unmodeled content (the SOUR citation itself, its DATA/TEXT) still round-trips; only
    // the consumed OBJE is pruned out, so it must not also survive inside the BIRT's residue -- otherwise
    // export would emit it a second time.
    var text = await ExportToTextAsync(document);
    text.Should().Contain("2 SOUR @S1@").And.Contain("4 TEXT Parish register");
    (text.Split("BLOB").Length - 1).Should().Be(1);
  }

  [Fact]
  public async Task NestedObjeUnderEventSour_ImageIsDiscoveredAsAttachment()
  {
    // Same nesting as above with an image-shaped OBJE: only a direct INDI-child OBJE is a portrait, so an
    // image nested under an event is documentary and lands as an attachment, never a photo.
    var blob = Convert.ToBase64String(Encoding.UTF8.GetBytes("baptism-photo"));
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Louis /Bourbon/\n1 SEX M\n" +
      "1 BIRT\n2 DATE 5 SEP 1638\n2 SOUR @S1@\n3 OBJE\n4 FORM jpeg\n" +
      $"4 BLOB {blob}\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().BeNull();
    full.AdditionalPhotos.Should().BeEmpty();
    var attachment = full.Attachments.Should().ContainSingle().Which;
    Encoding.UTF8.GetString(GedcomPhotoResidue.ExtractImageBytes(attachment.Content)).Should().Be("baptism-photo");
  }

  [Fact]
  public async Task MultiFileObjeUnderEventSour_SplitsIntoTwoAttachmentsAndStaysStableOnReExport()
  {
    // The Bourbon baptism shape: one OBJE under BIRT -> SOUR references two files sharing one TITL -- a jpg
    // scan and a pdf transcription. Both are nested under an event, so both surface as attachments (never a
    // photo), and a re-export must not grow the media on a second round-trip (the shared TITL is copied onto
    // each split object, so this pins that the duplication is stable rather than compounding).
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(mediaDir, "actes"));
    var scanBytes = new byte[] { 1, 2, 3 };
    var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "scan.jpg"), scanBytes, Token);
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "transcript.pdf"), pdfBytes, Token);
    try
    {
      var ged =
        "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Louis /Bourbon/\n1 SEX M\n" +
        "1 BIRT\n2 DATE 5 SEP 1638\n2 SOUR @S1@\n3 OBJE\n4 TITL Acte de Bapteme\n" +
        "4 FILE actes/scan.jpg\n5 FORM jpg\n4 FILE actes/transcript.pdf\n5 FORM pdf\n0 TRLR\n";
      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);

      var person = (await document.Persons.GetPersonsAsync(Token)).Single();
      var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);

      // Both files land as attachments (never a photo); the jpg keeps the shared TITL.
      full.MainPhoto.Should().BeNull();
      full.AdditionalPhotos.Should().BeEmpty();
      full.Attachments.Should().HaveCount(2);
      var scan = await AttachmentByFileAsync(full.Attachments, "actes/scan.jpg");
      GedcomPhotoResidue.ExtractImageBytes(scan.Content).Should().Equal(scanBytes);
      (await AttachmentTitleAsync(scan)).Should().Be("Acte de Bapteme");
      var transcript = await AttachmentByFileAsync(full.Attachments, "actes/transcript.pdf");
      GedcomPhotoResidue.ExtractImageBytes(transcript.Content).Should().Equal(pdfBytes);

      // Round-trip stability: export, reimport that export, export again -- the media count must not grow.
      var firstExport = await ExportToTextAsync(document);
      await using var roundTrip = await NewDocumentAsync();
      await _importer.ImportAsync(roundTrip, new StringReader(firstExport), Token);
      var secondExport = await ExportToTextAsync(roundTrip);
      firstExport.Split("2 BLOB").Length.Should().Be(3);
      secondExport.Split("2 BLOB").Length.Should().Be(firstExport.Split("2 BLOB").Length);
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
  }

  [Fact]
  public async Task EventNestedImage_LandsAsAttachmentAndLeavesTopLevelPortraitAsMain()
  {
    // The Kennedy Herald-Tribune shape: a direct-INDI portrait plus an image OBJE nested under DEAT (a
    // newspaper documenting the death, not a face). Only the direct child is a photo; the death document is
    // an attachment, and the portrait is untouched as the main.
    var portrait = Convert.ToBase64String(Encoding.UTF8.GetBytes("portrait"));
    var deathDocument = Convert.ToBase64String(Encoding.UTF8.GetBytes("herald-tribune"));
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME John /Kennedy/\n1 SEX M\n" +
      $"1 OBJE\n2 FORM jpeg\n2 BLOB {portrait}\n" +
      $"1 DEAT\n2 DATE 22 NOV 1963\n2 OBJE\n3 FORM jpeg\n3 BLOB {deathDocument}\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    Encoding.UTF8.GetString(full.MainPhoto!.Content).Should().Be("portrait");
    full.AdditionalPhotos.Should().BeEmpty();
    var attachment = full.Attachments.Should().ContainSingle().Which;
    Encoding.UTF8.GetString(GedcomPhotoResidue.ExtractImageBytes(attachment.Content)).Should().Be("herald-tribune");
  }

  [Fact]
  public async Task MarriageRecordObje_SurfacesOnBothSpousesSharingOneDataRow()
  {
    // A marriage certificate (jpg scan + pdf transcription) sits under the FAM record's MARR -> SOUR -> OBJE.
    // It is about the couple, and GT4 has no couple entity, so it surfaces on both spouses. A family OBJE is
    // documentary, so both files land as attachments (never a photo). FAM records are rebuilt from the edge
    // graph on export and carry no residue, so previously these files were dropped entirely. The bytes are
    // stored once and linked to both spouses (a shared Data row).
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(mediaDir, "actes"));
    var scanBytes = new byte[] { 1, 2, 3 };
    var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "mariage.jpg"), scanBytes, Token);
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "mariage.pdf"), pdfBytes, Token);
    try
    {
      var ged =
        "0 HEAD\n1 CHAR UTF-8\n" +
        "0 @I1@ INDI\n1 NAME Louis /Bourbon/\n1 SEX M\n" +
        "0 @I2@ INDI\n1 NAME Marie /Antoinette/\n1 SEX F\n" +
        "0 @F1@ FAM\n1 HUSB @I1@\n1 WIFE @I2@\n" +
        "1 MARR\n2 DATE 16 MAY 1770\n2 SOUR @S1@\n3 OBJE\n4 TITL Acte de mariage\n" +
        "4 FILE actes/mariage.jpg\n5 FORM jpg\n4 FILE actes/mariage.pdf\n5 FORM pdf\n0 TRLR\n";
      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);

      var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
      var louisFull = await document.PersonManager.GetPersonFullInfoAsync(byName["Louis Bourbon"], Token);
      var marieFull = await document.PersonManager.GetPersonFullInfoAsync(byName["Marie Antoinette"], Token);

      // Both files land as attachments on both spouses (never a photo); the jpg keeps the shared TITL.
      louisFull.MainPhoto.Should().BeNull();
      marieFull.MainPhoto.Should().BeNull();
      louisFull.AdditionalPhotos.Should().BeEmpty();
      marieFull.AdditionalPhotos.Should().BeEmpty();
      louisFull.Attachments.Should().HaveCount(2);
      marieFull.Attachments.Should().HaveCount(2);
      var louisScan = await AttachmentByFileAsync(louisFull.Attachments, "actes/mariage.jpg");
      var marieScan = await AttachmentByFileAsync(marieFull.Attachments, "actes/mariage.jpg");
      GedcomPhotoResidue.ExtractImageBytes(louisScan.Content).Should().Equal(scanBytes);
      (await AttachmentTitleAsync(louisScan)).Should().Be("Acte de mariage");
      var louisPdf = await AttachmentByFileAsync(louisFull.Attachments, "actes/mariage.pdf");
      var mariePdf = await AttachmentByFileAsync(marieFull.Attachments, "actes/mariage.pdf");
      GedcomPhotoResidue.ExtractImageBytes(louisPdf.Content).Should().Equal(pdfBytes);

      // Shared, not duplicated: the same Data row backs both spouses (same Id), so the bytes live once.
      louisScan.Id.Should().Be(marieScan.Id);
      louisPdf.Id.Should().Be(mariePdf.Id);

      // Export emits each spouse's media as its own INDI-level OBJE, so a round-trip de-shares the row into
      // two -- but stays bounded: the media count must not grow on a further hop.
      var firstExport = await ExportToTextAsync(document);
      await using var roundTrip = await NewDocumentAsync();
      await _importer.ImportAsync(roundTrip, new StringReader(firstExport), Token);
      var secondExport = await ExportToTextAsync(roundTrip);
      firstExport.Split("2 BLOB").Length.Should().Be(5);
      secondExport.Split("2 BLOB").Length.Should().Be(firstExport.Split("2 BLOB").Length);
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
  }

  [Fact]
  public async Task MarriageRecordObje_ReimportedFromSameSource_IsNotDuplicated()
  {
    // Re-importing the same source into a populated project must not add the marriage media a second time:
    // the undated spouses match on re-import, and the shared row is recognized as already present (the
    // encode is deterministic across imports). This is the gap-fill dedup the INDI path also guards.
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(mediaDir, "actes"));
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "mariage.jpg"), new byte[] { 1, 2, 3 }, Token);
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "mariage.pdf"), new byte[] { 0x25, 0x50, 0x44, 0x46 }, Token);
    try
    {
      var ged =
        "0 HEAD\n1 CHAR UTF-8\n" +
        "0 @I1@ INDI\n1 NAME Louis /Bourbon/\n1 SEX M\n" +
        "0 @I2@ INDI\n1 NAME Marie /Antoinette/\n1 SEX F\n" +
        "0 @F1@ FAM\n1 HUSB @I1@\n1 WIFE @I2@\n1 MARR\n2 DATE 16 MAY 1770\n2 SOUR @S1@\n3 OBJE\n" +
        "4 FILE actes/mariage.jpg\n5 FORM jpg\n4 FILE actes/mariage.pdf\n5 FORM pdf\n0 TRLR\n";
      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);
      var firstByName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
      var firstLouis = await document.PersonManager.GetPersonFullInfoAsync(firstByName["Louis Bourbon"], Token);
      var attachmentIds = firstLouis.Attachments.Select(a => a.Id).OrderBy(id => id).ToArray();
      attachmentIds.Should().HaveCount(2);

      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);

      var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
      byName.Should().HaveCount(2);
      var louis = await document.PersonManager.GetPersonFullInfoAsync(byName["Louis Bourbon"], Token);
      louis.AdditionalPhotos.Should().BeEmpty();
      louis.Attachments.Select(a => a.Id).OrderBy(id => id).Should().Equal(attachmentIds);
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
  }

  [Fact]
  public async Task MarriageRecordObje_ReachesMatchedSpousesThatHaveNoMediaYet()
  {
    // The tree-merge case: both spouses already exist from a prior media-less import. They match on the
    // second import but carry none of this media, so the certificate must still reach them -- the dedup keys
    // on content, not on whether the spouse is newly created, so a matched-but-empty spouse is not skipped.
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(mediaDir, "actes"));
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "actes", "mariage.jpg"), new byte[] { 1, 2, 3 }, Token);
    try
    {
      var head = "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Louis /Bourbon/\n1 SEX M\n" +
                 "0 @I2@ INDI\n1 NAME Marie /Antoinette/\n1 SEX F\n0 @F1@ FAM\n1 HUSB @I1@\n1 WIFE @I2@\n1 MARR\n2 DATE 16 MAY 1770\n";
      var gedNoMedia = head + "0 TRLR\n";
      var gedWithMedia = head + "2 SOUR @S1@\n3 OBJE\n4 FILE actes/mariage.jpg\n5 FORM jpg\n0 TRLR\n";

      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(gedNoMedia), Token, mediaDir);
      var before = await GedcomTestGraph.PersonsByNameAsync(document, Token);
      var louisBefore = await document.PersonManager.GetPersonFullInfoAsync(before["Louis Bourbon"], Token);
      louisBefore.Attachments.Should().BeEmpty();

      await _importer.ImportAsync(document, new StringReader(gedWithMedia), Token, mediaDir);

      var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
      byName.Should().HaveCount(2);
      var louis = await document.PersonManager.GetPersonFullInfoAsync(byName["Louis Bourbon"], Token);
      var marie = await document.PersonManager.GetPersonFullInfoAsync(byName["Marie Antoinette"], Token);
      var louisAttach = louis.Attachments.Should().ContainSingle().Which;
      var marieAttach = marie.Attachments.Should().ContainSingle().Which;
      louis.AdditionalPhotos.Should().BeEmpty();
      louisAttach.Id.Should().Be(marieAttach.Id);
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
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
  public async Task FileReferenceObje_LoadsExternalImageWhenBasePathGiven()
  {
    // An OBJE that points at an external image FILE (no embedded BLOB) is loaded into the person's photo set
    // when the .ged's folder is known. The FILE path is relative and uses a forward slash, as real files do.
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(mediaDir, "photos"));
    var mainBytes = new byte[] { 1, 2, 3, 4, 5 };
    var extraBytes = new byte[] { 9, 8, 7 };
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "photos", "main.png"), mainBytes, Token);
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "photos", "extra.png"), extraBytes, Token);
    try
    {
      var ged =
        "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
        "1 OBJE\n2 TITL Portrait\n2 FILE photos/main.png\n3 FORM png\n" +
        "1 OBJE\n2 FILE photos/extra.png\n3 FORM png\n0 TRLR\n";
      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);

      var person = (await document.Persons.GetPersonsAsync(Token)).Single();
      var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
      full.MainPhoto.Should().NotBeNull();
      // The main OBJE carries a TITL, so it is consumed as a tagged photo: Content is a
      // GedcomPhotoResidue envelope, not the raw image bytes directly.
      full.MainPhoto!.Category.Should().Be(DataCategory.PersonMainPhotoTagged);
      GedcomPhotoResidue.ExtractImageBytes(full.MainPhoto.Content).Should().Equal(mainBytes);
      full.MainPhoto.MimeType.Should().Be("image/png");
      (await GedcomPhotoResidue.ExtractTitleAsync(full.MainPhoto, Token)).Should().Be("Portrait");

      // The extra OBJE has no TITL/extra tags, so it stays a plain photo.
      var additionalPhoto = full.AdditionalPhotos.Should().ContainSingle().Which;
      additionalPhoto.Category.Should().Be(DataCategory.PersonPhoto);
      additionalPhoto.Content.Should().Equal(extraBytes);

      // The OBJEs were consumed as photos, so they are not also kept as opaque residue.
      full.GedcomData.Should().BeNull();

      // Export re-emits them self-contained as embedded BLOBs, with the main photo's TITL restored.
      var text = await ExportToTextAsync(document);
      text.Should().Contain("1 OBJE").And.Contain("2 FORM png").And.Contain("2 BLOB ").And.Contain("2 TITL Portrait");
      text.Should().NotContain("2 FILE");
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
  }

  [Fact]
  public async Task FileReferenceObje_ExternalImageIsReadThroughMediaReaderSeam()
  {
    // External media bytes come exclusively through IGedcomMediaReader: a substitute reader serves the
    // image with no file on disk, and is asked for the FILE reference resolved against the base path.
    var bytes = new byte[] { 4, 2 };
    var reader = new StubMediaReader(bytes);
    var importer = new GedcomImporter(reader);
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      "1 OBJE\n2 FILE photos/main.png\n3 FORM png\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await importer.ImportAsync(document, new StringReader(ged), Token, "base");

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().NotBeNull();
    full.MainPhoto!.Content.Should().Equal(bytes);
    reader.RequestedPath.Should().Be(Path.Combine("base", "photos", "main.png"));
  }

  [Fact]
  public async Task FileReferenceObje_NonImageLoadsAsAttachmentWhenBasePathGiven()
  {
    // A FILE that is not an image (a PDF scan) is never loaded into the photo set; instead it is loaded
    // into the attachment set when the base path is known and the file exists on disk.
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(mediaDir);
    var scanBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
    await File.WriteAllBytesAsync(Path.Combine(mediaDir, "scan.pdf"), scanBytes, Token);
    try
    {
      var ged =
        "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
        "1 OBJE\n2 FILE scan.pdf\n3 FORM pdf\n0 TRLR\n";
      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);

      var person = (await document.Persons.GetPersonsAsync(Token)).Single();
      var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
      full.MainPhoto.Should().BeNull();
      full.GedcomData.Should().BeNull();

      var attachment = full.Attachments.Should().ContainSingle().Which;
      attachment.MimeType.Should().Be("application/pdf");
      GedcomPhotoResidue.ExtractImageBytes(attachment.Content).Should().Equal(scanBytes);
      (await GedcomPhotoResidue.ExtractFileNameAsync(attachment, Token)).Should().Be("scan.pdf");
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
  }

  [Fact]
  public async Task FileReferenceObje_NonImageStaysResidueWithoutBasePath()
  {
    // Without a base path, a non-image FILE has no bytes GT4 can obtain either as a photo or an
    // attachment, so it survives verbatim as residue -- exactly like FileReferenceObje_FallsBackToResidue.
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      "1 OBJE\n2 FILE scan.pdf\n3 FORM pdf\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.Attachments.Should().BeEmpty();
    full.GedcomData.Should().NotBeNull();
  }

  [Fact]
  public async Task FileReferenceObje_UnreadableImageStaysResidueWithoutAbortingImport()
  {
    // An external image FILE that resolves on disk but cannot be read (here, held under an exclusive lock)
    // must not abort the all-or-nothing import: the unreadable OBJE falls back to residue, exactly like a
    // missing file, and the person is still imported.
    var mediaDir = Path.Combine(Path.GetTempPath(), $"gt4_media_{Guid.NewGuid():N}");
    Directory.CreateDirectory(mediaDir);
    var imagePath = Path.Combine(mediaDir, "locked.png");
    await File.WriteAllBytesAsync(imagePath, new byte[] { 1, 2, 3 }, Token);
    try
    {
      using var locked = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.None);

      var ged =
        "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
        "1 OBJE\n2 FILE locked.png\n3 FORM png\n0 TRLR\n";
      await using var document = await NewDocumentAsync();
      await _importer.ImportAsync(document, new StringReader(ged), Token, mediaDir);

      var person = (await document.Persons.GetPersonsAsync(Token)).Single();
      var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
      full.MainPhoto.Should().BeNull();
      full.GedcomData.Should().NotBeNull();

      var text = await ExportToTextAsync(document);
      text.Should().Contain("1 OBJE").And.Contain("2 FILE locked.png");
    }
    finally
    {
      Directory.Delete(mediaDir, recursive: true);
    }
  }

  [Fact]
  public async Task EmbeddedObje_CorruptBlobStaysResidueWithoutAbortingImport()
  {
    // A malformed base64 BLOB cannot be decoded, so the OBJE is not a photo; like an unreadable FILE it
    // falls back to residue instead of throwing and aborting the import.
    var ged =
      "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Foto /Test/\n1 SEX M\n" +
      "1 OBJE\n2 FORM jpeg\n2 BLOB not-valid-base64!!!\n0 TRLR\n";
    await using var document = await NewDocumentAsync();
    await _importer.ImportAsync(document, new StringReader(ged), Token);

    var person = (await document.Persons.GetPersonsAsync(Token)).Single();
    var full = await document.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().BeNull();
    full.GedcomData.Should().NotBeNull();

    var text = await ExportToTextAsync(document);
    text.Should().Contain("1 OBJE").And.Contain("2 BLOB ");
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

  private sealed class StubMediaReader(byte[] content) : IGedcomMediaReader
  {
    public string? RequestedPath { get; private set; }

    public byte[]? TryRead(string path)
    {
      RequestedPath = path;
      return content;
    }
  }
}
