using FluentAssertions;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Text;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

/// <summary>
/// Exercises export + re-import against real on-disk <see cref="ProjectDocument"/>s. The crux is that
/// GT4 stores parent/child and marriage as pairwise edges while GEDCOM groups them into families: the
/// reconstructed family keys must be identical on both paths, or families split/duplicate and the
/// re-imported edge graph diverges from the original.
/// </summary>
public sealed class GedcomRoundTripTests : IAsyncLifetime
{
  private readonly List<string> _paths = [];
  private ProjectDocument _source = null!;
  private static CancellationToken Token => TestContext.Current.CancellationToken;

  private readonly GedcomExporter _exporter = new();
  private readonly GedcomImporter _importer = new(new FileGedcomMediaReader());

  public async ValueTask InitializeAsync()
  {
    _source = await NewDocumentAsync();
  }

  private async Task<ProjectDocument> NewDocumentAsync()
  {
    var path = Path.Combine(Path.GetTempPath(), $"gt4_gedcom_{Guid.NewGuid():N}.db");
    _paths.Add(path);
    return await ProjectDocument.CreateNewAsync(path, "gedcom", Token);
  }

  public async ValueTask DisposeAsync()
  {
    await _source.DisposeAsync();
    foreach (var path in _paths)
    {
      foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
      {
        try { File.Delete(path + suffix); } catch { /* best-effort temp cleanup */ }
      }
    }
  }

  private static Date Year(int year) => Date.Create(year * 10_000 + 101, DateStatus.WellKnown);

  private async Task<Person> AddPersonAsync(string firstName, BiologicalSex sex, Date birth, Date? death = null)
  {
    var name = await _source.Names.AddNameAsync(firstName, NameType.FirstName, null, Token);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = birth,
      DeathDate = death,
      BiologicalSex = sex,
      Names = [name],
    };
    var added = await _source.PersonManager.AddPersonAsync(info, Token);
    return new Person(added.Id, added.BirthDate, added.DeathDate, added.BiologicalSex);
  }

  private Task MarryAsync(Person husband, Person wife, Date when) =>
    _source.Relatives.AddRelativesAsync(husband, [new Relative(wife, RelationshipType.Spouse, when)], Token);

  private async Task AddChildAsync(Person child, params Person[] parents)
  {
    foreach (var parent in parents)
    {
      await _source.Relatives.AddRelativesAsync(parent, [new Relative(child, RelationshipType.Child, null)], Token);
    }
  }

  private async Task AddAdoptedChildAsync(Person child, params Person[] parents)
  {
    foreach (var parent in parents)
    {
      await _source.Relatives.AddRelativesAsync(parent, [new Relative(child, RelationshipType.AdoptiveChild, null)], Token);
    }
  }

  [Fact]
  public async Task FullGraph_SurvivesExportAndReimport()
  {
    // A married couple with two children.
    var fa = await AddPersonAsync("Fa", BiologicalSex.Male, Year(1900));
    var mo = await AddPersonAsync("Mo", BiologicalSex.Female, Year(1905));
    await MarryAsync(fa, mo, Year(1925));
    var ka = await AddPersonAsync("Ka", BiologicalSex.Male, Year(1926));
    var kb = await AddPersonAsync("Kb", BiologicalSex.Female, Year(1928));
    await AddChildAsync(ka, fa, mo);
    await AddChildAsync(kb, fa, mo);

    // The mother remarries and has a child with the second husband.
    var fb = await AddPersonAsync("Fb", BiologicalSex.Male, Year(1903));
    await MarryAsync(fb, mo, Year(1940));
    var kc = await AddPersonAsync("Kc", BiologicalSex.Male, Year(1941));
    await AddChildAsync(kc, fb, mo);

    // A single parent with one child (no recorded partner).
    var sp = await AddPersonAsync("Sp", BiologicalSex.Female, Year(1950));
    var sc = await AddPersonAsync("Sc", BiologicalSex.Male, Year(1970));
    await AddChildAsync(sc, sp);

    // A childless married couple.
    var ca = await AddPersonAsync("Ca", BiologicalSex.Male, Year(1880));
    var cb = await AddPersonAsync("Cb", BiologicalSex.Female, Year(1885));
    await MarryAsync(ca, cb, Year(1910));

    var expected = await GedcomTestGraph.ExtractAsync(_source, Token);

    var text = await ExportToTextAsync(_source);

    // Structural guard: the edge-graph comparison below round-trips even if a couple were wrongly split
    // across two FAM records, because import re-derives the same edges from each. Assert the family
    // shape directly — exactly four families (two couples-with-kids, one single parent, one childless
    // couple) and no individual pointing at the same spouse-family twice.
    var records = await GedcomReader.ReadAsync(new StringReader(text), Token);
    records.Count(r => r.Tag == "FAM").Should().Be(4);
    foreach (var individual in records.Where(r => r.Tag == "INDI"))
    {
      var spouseFamilies = individual.ChildrenWithTag("FAMS").Select(f => f.Value).ToList();
      spouseFamilies.Should().OnlyHaveUniqueItems();
    }

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var actual = await GedcomTestGraph.ExtractAsync(reimported, Token);

    actual.Should().BeEquivalentTo(expected);
  }

  [Fact]
  public async Task AdoptiveRelationships_RoundTripAndMarkPedigree()
  {
    // A couple with one birth child and one adopted child: both belong to the same family, but the
    // adopted child's FAMC link must carry "PEDI adopted".
    var father = await AddPersonAsync("Pa", BiologicalSex.Male, Year(1900));
    var mother = await AddPersonAsync("Ma", BiologicalSex.Female, Year(1902));
    await MarryAsync(father, mother, Year(1925));
    var birthChild = await AddPersonAsync("Bc", BiologicalSex.Male, Year(1926));
    var adoptedChild = await AddPersonAsync("Ac", BiologicalSex.Female, Year(1930));
    await AddChildAsync(birthChild, father, mother);
    await AddAdoptedChildAsync(adoptedChild, father, mother);

    // A single adoptive parent.
    var soleParent = await AddPersonAsync("So", BiologicalSex.Female, Year(1950));
    var soleAdopted = await AddPersonAsync("Sd", BiologicalSex.Male, Year(1975));
    await AddAdoptedChildAsync(soleAdopted, soleParent);

    var expected = await GedcomTestGraph.ExtractAsync(_source, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain($"2 {GedcomTags.Pedigree} {GedcomTags.AdoptedPedigree}");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var actual = await GedcomTestGraph.ExtractAsync(reimported, Token);

    actual.Should().BeEquivalentTo(expected);
  }

  [Fact]
  public async Task PersonFields_NameSexBirthDeathAndBio_RoundTrip()
  {
    var name = await _source.Names.AddNameAsync("Solo", NameType.FirstName, null, Token);
    var bio = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("A short life story."), "text/plain", DataCategory.PersonBio);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Date.Create(18500304, DateStatus.WellKnown),
      DeathDate = Date.Create(19200101, DateStatus.WellKnown),
      BiologicalSex = BiologicalSex.Female,
      Names = [name],
      Biography = bio,
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var persons = await reimported.Persons.GetPersonsAsync(Token);
    var person = persons.Single();
    person.BiologicalSex.Should().Be(BiologicalSex.Female);
    person.BirthDate.Should().Be(Date.Create(18500304, DateStatus.WellKnown));
    person.DeathDate.Should().Be(Date.Create(19200101, DateStatus.WellKnown));

    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.DisplayName.Should().Be("Solo");
    full.Biography.Should().NotBeNull();
    Encoding.UTF8.GetString(full.Biography!.Content).Should().Be("A short life story.");
  }

  [Fact]
  public async Task Biography_PersonLinkSyntax_RoundTrips()
  {
    var name = await _source.Names.AddNameAsync("Solo", NameType.FirstName, null, Token);
    var bio = new Data(
      ElementId.NonCommittedId,
      Encoding.UTF8.GetBytes("See [Jane Doe](person:123) for details."),
      "text/plain",
      DataCategory.PersonBio);
    var info = PersonFullInfo.Empty with { Names = [name], Biography = bio };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    Encoding.UTF8.GetString(full.Biography!.Content).Should().Be("See [Jane Doe](person:123) for details.");
  }

  [Fact]
  public async Task LivingPerson_HasNoDeathEventAndStaysAliveOnReimport()
  {
    // A person with no death date is alive: export must not emit a bare DEAT for them, or reimport would
    // turn the empty death event back into an (unknown) death date and silently bury the living.
    await AddPersonAsync("Alive", BiologicalSex.Male, Year(1990));

    var text = await ExportToTextAsync(_source);
    text.Should().NotContain($"1 {GedcomTags.Death}");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    person.DeathDate.Should().BeNull();
  }

  [Fact]
  public async Task DeathWithUnknownDate_EmitsBareDeathEventAndSurvivesReimportAsUnknown()
  {
    // A DeathDate with Status.Unknown means "known dead, date not known" -- distinct from no DeathDate
    // at all (LivingPerson_... above). Round-tripping must keep that distinction, not collapse it to alive.
    await AddPersonAsync("Departed", BiologicalSex.Male, Year(1900), Date.Create(0, DateStatus.Unknown));

    var text = await ExportToTextAsync(_source);
    text.Should().Contain($"1 {GedcomTags.Death}");
    text.Should().NotContain($"1 {GedcomTags.Death}\n2 {GedcomTags.Date}");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    person.DeathDate.Should().NotBeNull();
    person.DeathDate!.Value.Status.Should().Be(DateStatus.Unknown);
  }

  [Fact]
  public async Task Export_EmitsHeaderEnvelopeAndTrailer()
  {
    await AddPersonAsync("Lonely", BiologicalSex.Unknown, Year(1800));

    var text = await ExportToTextAsync(_source);

    text.Should().Contain("0 HEAD");
    text.Should().Contain("2 VERS 5.5.1");
    text.Should().Contain("1 CHAR UTF-8");
    text.TrimEnd().Should().EndWith("0 TRLR");
  }

  [Fact]
  public async Task Photos_RoundTripAsEmbeddedObje()
  {
    var name = await _source.Names.AddNameAsync("Foto", NameType.FirstName, null, Token);
    var main = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("MAIN-IMAGE-BYTES"), "image/jpeg", DataCategory.PersonMainPhoto);
    var extraOne = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("EXTRA-ONE"), "image/png", DataCategory.PersonPhoto);
    var extraTwo = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("EXTRA-TWO"), "image/bmp", DataCategory.PersonPhoto);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      BiologicalSex = BiologicalSex.Male,
      Names = [name],
      MainPhoto = main,
      AdditionalPhotos = [extraOne, extraTwo],
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);

    // Embedded and self-contained: OBJE with the format, the main photo marked primary, and the bytes
    // base64-encoded into a BLOB — never raw binary.
    text.Should().Contain("1 OBJE").And.Contain("2 FORM jpeg").And.Contain("2 _PRIM Y").And.Contain("2 BLOB ");
    text.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("MAIN-IMAGE-BYTES")));

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    full.MainPhoto.Should().NotBeNull();
    full.MainPhoto!.MimeType.Should().Be("image/jpeg");
    Encoding.UTF8.GetString(full.MainPhoto.Content).Should().Be("MAIN-IMAGE-BYTES");

    full.AdditionalPhotos.Should().HaveCount(2);
    full.AdditionalPhotos.Select(p => Encoding.UTF8.GetString(p.Content)).Should().BeEquivalentTo("EXTRA-ONE", "EXTRA-TWO");
    full.AdditionalPhotos.Select(p => p.MimeType).Should().BeEquivalentTo("image/png", "image/bmp");
  }

  [Fact]
  public async Task TaggedPhoto_TitleSurvivesExportThenReimportThenReexport()
  {
    var name = await _source.Names.AddNameAsync("Tagged", NameType.FirstName, null, Token);
    var residual = new GedcomNode { Tag = "OBJE" };
    residual.Add(new GedcomNode { Tag = "TITL", Value = "Louis XIII par Rubens" });
    var imageBytes = Encoding.UTF8.GetBytes("PORTRAIT-BYTES");
    var content = GedcomPhotoResidue.Encode(imageBytes, residual);
    var main = new Data(ElementId.NonCommittedId, content, "image/jpeg", DataCategory.PersonMainPhotoTagged);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      MainPhoto = main,
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain("2 TITL Louis XIII par Rubens");
    text.Should().Contain(Convert.ToBase64String(imageBytes));

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    full.MainPhoto.Should().NotBeNull();
    full.MainPhoto!.Category.Should().Be(DataCategory.PersonMainPhotoTagged);
    GedcomPhotoResidue.ExtractImageBytes(full.MainPhoto.Content).Should().Equal(imageBytes);
    (await GedcomPhotoResidue.ExtractTitleAsync(full.MainPhoto, Token)).Should().Be("Louis XIII par Rubens");

    // A second export/reimport hop must still carry the title -- proves this isn't a one-shot fluke.
    var reexportedText = await ExportToTextAsync(reimported);
    reexportedText.Should().Contain("2 TITL Louis XIII par Rubens");
  }

  [Fact]
  public async Task AdditionalPhotos_MixOfPlainAndTagged_BothSurviveExportAndReimport()
  {
    // Exercises GedcomExporter.GetPhotosByCategoriesAsync's concatenation branch: a person present in
    // both the PersonPhoto and PersonPhotoTagged dictionaries at once (one additional photo of each
    // kind), not just the overwrite case where only one side has an entry.
    var name = await _source.Names.AddNameAsync("Mixed", NameType.FirstName, null, Token);
    var residual = new GedcomNode { Tag = "OBJE" };
    residual.Add(new GedcomNode { Tag = "TITL", Value = "Family gathering" });
    var taggedContent = GedcomPhotoResidue.Encode(Encoding.UTF8.GetBytes("TAGGED-EXTRA"), residual);
    var main = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("MAIN-IMAGE"), "image/jpeg", DataCategory.PersonMainPhoto);
    var plainExtra = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("PLAIN-EXTRA"), "image/png", DataCategory.PersonPhoto);
    var taggedExtra = new Data(ElementId.NonCommittedId, taggedContent, "image/jpeg", DataCategory.PersonPhotoTagged);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      MainPhoto = main,
      AdditionalPhotos = [plainExtra, taggedExtra],
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("PLAIN-EXTRA")));
    text.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("TAGGED-EXTRA")));
    text.Should().Contain("2 TITL Family gathering");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    full.AdditionalPhotos.Should().HaveCount(2);
    var plain = full.AdditionalPhotos.Should().ContainSingle(p => p.Category == DataCategory.PersonPhoto).Which;
    Encoding.UTF8.GetString(plain.Content).Should().Be("PLAIN-EXTRA");
    var tagged = full.AdditionalPhotos.Should().ContainSingle(p => p.Category == DataCategory.PersonPhotoTagged).Which;
    GedcomPhotoResidue.ExtractImageBytes(tagged.Content).Should().Equal(Encoding.UTF8.GetBytes("TAGGED-EXTRA"));
    (await GedcomPhotoResidue.ExtractTitleAsync(tagged, Token)).Should().Be("Family gathering");
  }

  [Fact]
  public async Task Photo_LargePayloadChunksAcrossConcLinesAndRoundTrips()
  {
    // A real photo is multi-KB, so its base64 always exceeds the writer's per-line cap and is split across
    // CONC continuation lines. This is the production path the small-payload tests never reach: it must
    // chunk on write and reassemble byte-for-byte on read.
    var bytes = new byte[4096];
    new Random(42).NextBytes(bytes);
    var name = await _source.Names.AddNameAsync("Big", NameType.FirstName, null, Token);
    var main = new Data(ElementId.NonCommittedId, bytes, "image/jpeg", DataCategory.PersonMainPhoto);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      MainPhoto = main,
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain("\n3 CONC ", "a multi-KB photo's base64 must be split across CONC lines");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto!.Content.Should().Equal(bytes);
  }

  [Fact]
  public async Task Photo_NullMimeTypeRoundTripsAsNull()
  {
    // A picked file can carry no content type, so Data.MimeType is null. Export must not invent a format,
    // and import must give it back as null rather than fabricating one.
    var name = await _source.Names.AddNameAsync("NoMime", NameType.FirstName, null, Token);
    var main = new Data(ElementId.NonCommittedId, Encoding.UTF8.GetBytes("BYTES"), null, DataCategory.PersonMainPhoto);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      MainPhoto = main,
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain("1 OBJE").And.Contain("2 BLOB ");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);
    full.MainPhoto.Should().NotBeNull();
    full.MainPhoto!.MimeType.Should().BeNull();
    Encoding.UTF8.GetString(full.MainPhoto.Content).Should().Be("BYTES");
  }

  [Fact]
  public async Task Attachment_FileNameAndBytesSurviveExportThenReimportThenReexport()
  {
    var name = await _source.Names.AddNameAsync("Scan", NameType.FirstName, null, Token);
    var residual = new GedcomNode { Tag = "OBJE" };
    residual.Add(new GedcomNode { Tag = "FILE", Value = "birth-certificate.pdf" });
    var fileBytes = Encoding.UTF8.GetBytes("PDF-BYTES");
    var content = GedcomPhotoResidue.Encode(fileBytes, residual);
    var attachment = new Data(ElementId.NonCommittedId, content, "application/pdf", DataCategory.PersonAttachment);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      Attachments = [attachment],
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain("1 OBJE").And.Contain("2 FORM pdf").And.Contain("2 FILE birth-certificate.pdf").And.Contain("2 BLOB ");
    text.Should().NotContain("_PRIM");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    // The attachment's embedded BLOB must not also be picked up as a (broken) photo.
    full.MainPhoto.Should().BeNull();
    full.AdditionalPhotos.Should().BeEmpty();

    var reimportedAttachment = full.Attachments.Should().ContainSingle().Which;
    reimportedAttachment.Category.Should().Be(DataCategory.PersonAttachment);
    reimportedAttachment.MimeType.Should().Be("application/pdf");
    GedcomPhotoResidue.ExtractImageBytes(reimportedAttachment.Content).Should().Equal(fileBytes);
    (await GedcomPhotoResidue.ExtractFileNameAsync(reimportedAttachment, Token)).Should().Be("birth-certificate.pdf");

    // A second export/reimport hop must still carry the filename -- proves this isn't a one-shot fluke.
    var reexportedText = await ExportToTextAsync(reimported);
    reexportedText.Should().Contain("2 FILE birth-certificate.pdf");
  }

  [Fact]
  public async Task Attachment_WithImageMimeType_SurvivesExportThenReimportAsAnAttachmentNotAPhoto()
  {
    // An attachment whose underlying file happens to be an image (e.g. a scanned photo saved as a
    // generic attachment rather than via the photo picker) is indistinguishable from a real photo by
    // FORM/FILE alone -- the _ATTACH marker is what keeps it classified as an attachment on reimport.
    var name = await _source.Names.AddNameAsync("Scan", NameType.FirstName, null, Token);
    var residual = new GedcomNode { Tag = "OBJE" };
    residual.Add(new GedcomNode { Tag = "FILE", Value = "scan.png" });
    var fileBytes = Encoding.UTF8.GetBytes("PNG-BYTES");
    var content = GedcomPhotoResidue.Encode(fileBytes, residual);
    var attachment = new Data(ElementId.NonCommittedId, content, "image/png", DataCategory.PersonAttachment);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      Attachments = [attachment],
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);
    text.Should().Contain("_ATTACH Y");

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    full.MainPhoto.Should().BeNull();
    full.AdditionalPhotos.Should().BeEmpty();
    var reimportedAttachment = full.Attachments.Should().ContainSingle().Which;
    reimportedAttachment.Category.Should().Be(DataCategory.PersonAttachment);
    reimportedAttachment.MimeType.Should().Be("image/png");
    GedcomPhotoResidue.ExtractImageBytes(reimportedAttachment.Content).Should().Equal(fileBytes);
    (await GedcomPhotoResidue.ExtractFileNameAsync(reimportedAttachment, Token)).Should().Be("scan.png");
  }

  [Fact]
  public async Task Attachment_BuiltViaEncodeAttachment_SurvivesExportThenReimportAsAnAttachmentNotAPhoto()
  {
    // Mirrors what the UI file picker does: a freshly picked file, never previously round-tripped through
    // GEDCOM, so its envelope is synthesized via EncodeAttachment rather than carrying an imported residual.
    var name = await _source.Names.AddNameAsync("Scan", NameType.FirstName, null, Token);
    var fileBytes = Encoding.UTF8.GetBytes("PDF-BYTES");
    var content = GedcomPhotoResidue.EncodeAttachment(fileBytes, "deed.pdf");
    var attachment = new Data(ElementId.NonCommittedId, content, "application/pdf", DataCategory.PersonAttachment);
    var info = PersonFullInfo.Empty with
    {
      BirthDate = Year(1900),
      Names = [name],
      Attachments = [attachment],
    };
    await _source.PersonManager.AddPersonAsync(info, Token);

    var text = await ExportToTextAsync(_source);

    await using var reimported = await NewDocumentAsync();
    await _importer.ImportAsync(reimported, new StringReader(text), Token);

    var person = (await reimported.Persons.GetPersonsAsync(Token)).Single();
    var full = await reimported.PersonManager.GetPersonFullInfoAsync(person, Token);

    // A non-null, non-image MimeType is what keeps this an attachment on reimport rather than a photo --
    // a null or image-shaped MimeType here would emit no/an image FORM and reclassify as a photo instead.
    full.MainPhoto.Should().BeNull();
    var reimportedAttachment = full.Attachments.Should().ContainSingle().Which;
    reimportedAttachment.Category.Should().Be(DataCategory.PersonAttachment);
    GedcomPhotoResidue.ExtractImageBytes(reimportedAttachment.Content).Should().Equal(fileBytes);
    (await GedcomPhotoResidue.ExtractFileNameAsync(reimportedAttachment, Token)).Should().Be("deed.pdf");
  }

  private async Task<string> ExportToTextAsync(ProjectDocument document)
  {
    var writer = new StringWriter();
    await _exporter.ExportAsync(document, writer, Token);
    return writer.ToString();
  }

}
