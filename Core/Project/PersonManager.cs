using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public class PersonManager : TableBase
{
  private static readonly ElementIdComparer<RelativeInfo> _RelativeInfoComparer = new();

  public PersonManager(ProjectDocument document)
    : base(document: document)
  {
  }

  private async Task<PersonInfo> CreatePersonInfoAsync(Person person, bool selectMainPhoto, CancellationToken token)
  {
    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var mainPhoto = selectMainPhoto
      ? Document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonMainPhoto, token)
      : Task.FromResult(Array.Empty<Data>());
    await Task.WhenAll(names, mainPhoto);

    var ret = new PersonInfo(person, names.Result, mainPhoto.Result.FirstOrDefault());
    return ret;
  }

  private static Data[] CombinePersonData(PersonFullInfo personFullInfo)
  {
    var personDataSet = new List<Data>(personFullInfo.AdditionalPhotos);

    if (personFullInfo.MainPhoto is not null)
    {
      personDataSet.Add(personFullInfo.MainPhoto);
    }
    if (personFullInfo.Biography is not null)
    {
      personDataSet.Add(personFullInfo.Biography);
    }

    return personDataSet.ToArray();
  }

  public async Task<PersonFullInfo> GetPersonFullInfoAsync(Person person, CancellationToken token)
  {
    if (person.Id == NonCommitedId)
    {
      throw new ArgumentException("person is not commited");
    }

    const bool selectMainPhoto = true;
    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var personData = Document.PersonData.GetPersonDataSetAsync(person, null, token);
    var relatives = Document.Relatives.GetRelativesAsync(person, token);
    await Task.WhenAll(names, personData, relatives);
    var relativePersonInfos = await GetPersonInfosAsync(
      persons: relatives.Result,
      selectMainPhoto: selectMainPhoto,
      token: token);

    var relativeInfos = new RelativeInfo[relativePersonInfos.Length];
    for (var i = 0; i < relativePersonInfos.Length; i++)
    {
      var relative = relatives.Result[i];
      var relativePerson = relativePersonInfos[i];
      relativeInfos[i] = new RelativeInfo(relative, relativePerson.Names, relativePerson.MainPhoto);
    }

    var ret = new PersonFullInfo(
      person: person,
      names: names.Result,
      mainPhoto: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonMainPhoto),
      additionalPhotos: personData.Result.Where(data => data.Category == DataCategory.PersonPhoto).ToArray(),
      relativeInfos: relativeInfos,
      biography: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonBio));

    return ret;
  }

  public async Task<PersonInfo[]> GetPersonInfosAsync(bool selectMainPhoto, CancellationToken token)
  {
    var persons = await Document.Persons.GetPersonsAsync(token);
    var ret = await GetPersonInfosAsync(persons, selectMainPhoto, token);

    return ret;
  }

  public async Task<PersonInfo[]> GetPersonInfosAsync(Person[] persons, bool selectMainPhoto, CancellationToken token)
  {
    var ret = await Task.WhenAll(persons.Select(person => CreatePersonInfoAsync(person, selectMainPhoto, token)));
    return ret;
  }

  public async Task<PersonInfo[]> GetPersonInfosByNameAsync(Name name, bool selectMainPhoto, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      SELECT Id
      FROM Persons
      INNER JOIN
        PersonNames ON PersonNames.PersonId=Persons.Id
      WHERE PersonNames.NameId=@id;
      """;
    command.Parameters.AddWithValue("@id", name.Id);
    await using var reader = await command.ExecuteReaderAsync(token);

    var tasks = new List<Task<Person?>>();
    while (await reader.ReadAsync(token))
    {
      var task = Document.Persons.TryGetPersonByIdAsync(reader.GetInt32(0), token);
      tasks.Add(task);
    }

    var persons = await Task.WhenAll(tasks);
    var ret = await Task.WhenAll(persons
      .Where(person => person is not null)
      .Select(person => CreatePersonInfoAsync(person!, selectMainPhoto, token)));

    return ret ?? [];
  }

  public async Task<PersonInfo> AddPersonAsync(PersonFullInfo personFullInfo, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    var familyName = personFullInfo.Names.SingleOrDefault(name => name.Type == NameType.FamilyName);
    if (familyName is not null)
    {
      var requiredFamilyNames = await Document.FamilyManager.GetRequiredNames(familyName, personFullInfo, token);
      personFullInfo = personFullInfo with { Names = personFullInfo.Names.Concat(requiredFamilyNames).ToArray() };
    }
    var person = await Document.Persons.AddPersonAsync(personFullInfo, token);

    await Task.WhenAll(
      Document.PersonNames.AddPersonNamesAsync(person, personFullInfo.Names, token),
      Document.PersonData.AddPersonDataSetAsync(person, CombinePersonData(personFullInfo), token),
      Document.Relatives.AddRelativesAsync(person, personFullInfo.RelativeInfos, token));

    transaction.Commit();

    return personFullInfo with { Id = person.Id };
  }

  public async Task UpdatePersonAsync(PersonFullInfo personFullInfo, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    await Document.Persons.UpdatePersonAsync(personFullInfo, token);
    await Task.WhenAll(
      Document.PersonNames.UpdatePersonNamesAsync(personFullInfo, personFullInfo.Names, token),
      Document.PersonData.UpdatePersonDataSetAsync(personFullInfo, CombinePersonData(personFullInfo), token),
      Document.Relatives.UpdateRelativesAsync(personFullInfo, personFullInfo.RelativeInfos, token));

    transaction.Commit();
  }

  public async Task<Siblings> GetSiblings(PersonFullInfo info, CancellationToken token)
  {
    var tasks = info
      .RelativeInfos
      .Where(r => r.Type == RelationshipType.Parent || r.Type == RelationshipType.AdoptiveParent)
      .Select(r => GetPersonFullInfoAsync(r, token));

    var parents = await Task.WhenAll(tasks);

    bool IsSibling(RelativeInfo relativeInfo)
    {
      var ret = relativeInfo switch
      {
        _ when relativeInfo.Id == info.Id => false,
        _ when relativeInfo.Type == RelationshipType.Child => true,
        _ when relativeInfo.Type == RelationshipType.AdoptiveChild => true,
        _ => false
      };

      return ret;
    }

    return new SiblingsInfo
    (
      person: info,
      relatives: parents.ToDictionary(p => p.Id, p => p.RelativeInfos.Where(IsSibling).ToArray())
    );
  }

  public static RelativeInfo? Mother(PersonFullInfo info) =>
    info
    .RelativeInfos
    .SingleOrDefault(r => r.Type == RelationshipType.Parent && r.BiologicalSex == BiologicalSex.Female);

  public static RelativeInfo? Father(PersonFullInfo info) =>
    info
    .RelativeInfos
    .SingleOrDefault(r => r.Type == RelationshipType.Parent && r.BiologicalSex == BiologicalSex.Male);

  public static RelativeInfo[] Sons(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.Child && r.BiologicalSex == BiologicalSex.Male)
    .ToArray();

  public static RelativeInfo[] Daughters(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.Child && r.BiologicalSex == BiologicalSex.Female)
    .ToArray();

  public static RelativeInfo[] AdoptiveFathers(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveParent && r.BiologicalSex == BiologicalSex.Male)
    .ToArray();

  public static RelativeInfo[] AdoptiveMothers(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveParent && r.BiologicalSex == BiologicalSex.Female)
    .ToArray();

  public static RelativeInfo[] AdoptiveSons(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveChild && r.BiologicalSex == BiologicalSex.Male)
    .ToArray();

  public static RelativeInfo[] AdoptiveDaughters(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveChild && r.BiologicalSex == BiologicalSex.Female)
    .ToArray();

  public static RelativeInfo[] NativeSiblings(Siblings siblings, BiologicalSex? biologicalSex)
  {
    var info = (SiblingsInfo)siblings;
    var mother = Mother(info.person);
    var father = Father(info.person);
    if (mother is null || father is null)
    {
      return [];
    }

    var siblingsByFather = info.relatives[father.Id];
    var siblingsByMother = info.relatives[mother.Id];
    var ret = siblingsByFather
      .Intersect(siblingsByMother, _RelativeInfoComparer)
      .Where(r => biologicalSex is null || r.BiologicalSex == biologicalSex)
      .ToArray();

    return ret;
  }

  public static RelativeInfo[] SiblingsByFather(Siblings siblings, BiologicalSex? biologicalSex)
  {
    var info = (SiblingsInfo)siblings;
    var father = Father(info.person);
    var mother = Mother(info.person);
    if (father is null)
    {
      return [];
    }

    var siblingsByFather = info.relatives[father.Id];
    if (mother is null)
    {
      return siblingsByFather.ToArray();
    }

    var siblingsByMother = info.relatives[mother.Id];
    var ret = siblingsByFather
      .Except(siblingsByMother, _RelativeInfoComparer)
      .Where(r => biologicalSex is null || r.BiologicalSex == biologicalSex)
      .ToArray();

    return ret;
  }

  public static RelativeInfo[] SiblingsByMother(Siblings siblings, BiologicalSex? biologicalSex)
  {
    var info = (SiblingsInfo)siblings;
    var father = Father(info.person);
    var mother = Mother(info.person);
    if (mother is null)
    {
      return [];
    }

    var siblingsByMother = info.relatives[mother.Id];
    if (father is null)
    {
      return siblingsByMother.ToArray();
    }

    var siblingsByFather = info.relatives[father.Id];
    var ret = siblingsByMother
      .Except(siblingsByFather, _RelativeInfoComparer)
      .Where(r => biologicalSex is null || r.BiologicalSex == biologicalSex)
      .ToArray();

    return ret;
  }

  public static RelativeInfo[] AdoptiveSiblings(Siblings siblings, BiologicalSex? biologicalSex)
  {
    var info = (SiblingsInfo)siblings;
    var mother = Mother(info.person);
    var father = Father(info.person);

    var ret = info
      .relatives
      .Where(i => i.Key != mother?.Id && i.Key != father?.Id)
      .SelectMany(i => i.Value)
      .Where(r => biologicalSex is null || r.BiologicalSex == biologicalSex)
      .ToArray();

    return ret;
  }

  public override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }
}
