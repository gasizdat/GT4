using FluentAssertions;
using GT4.Core.Gedcom;
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
  private readonly GedcomImporter _importer = new();

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
    var records = GedcomReader.Read(new StringReader(text));
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
    var bio = new Data(TableBase.NonCommittedId, Encoding.UTF8.GetBytes("A short life story."), "text/plain", DataCategory.PersonBio);
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
  public async Task Export_EmitsHeaderEnvelopeAndTrailer()
  {
    await AddPersonAsync("Lonely", BiologicalSex.Unknown, Year(1800));

    var text = await ExportToTextAsync(_source);

    text.Should().Contain("0 HEAD");
    text.Should().Contain("2 VERS 5.5.1");
    text.Should().Contain("1 CHAR UTF-8");
    text.TrimEnd().Should().EndWith("0 TRLR");
  }

  private async Task<string> ExportToTextAsync(ProjectDocument document)
  {
    var writer = new StringWriter();
    await _exporter.ExportAsync(document, writer, Token);
    return writer.ToString();
  }

}
