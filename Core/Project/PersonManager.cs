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
      personFullInfo = personFullInfo with { Names = [.. personFullInfo.Names, .. requiredFamilyNames] };
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

  public async Task<Siblings> GetSiblings(PersonFullInfo personInfo, CancellationToken token)
  {
    async Task<(RelativeInfo Relative, PersonFullInfo Person)>
      GetRelativePersonFullInfoAsync(RelativeInfo relative, CancellationToken token) =>
        (Relative: relative, Person: await GetPersonFullInfoAsync(relative, token));

    var parentTasks = personInfo
      .RelativeInfos
      .Where(r => r.Type == RelationshipType.Parent)
      .Select(r => GetPersonFullInfoAsync(r, token));
    var adoptiveParentTasks = personInfo
      .RelativeInfos
      .Where(r => r.Type == RelationshipType.AdoptiveParent)
      .Select(r => GetRelativePersonFullInfoAsync(r, token));
    var parentsTasks = Task.WhenAll(parentTasks);
    var adoptiveParentsTasks = Task.WhenAll(adoptiveParentTasks);
    await Task.WhenAll(parentsTasks, adoptiveParentsTasks);
    var parents = parentsTasks.Result;
    var adoptiveParents = adoptiveParentsTasks.Result;
    var allMotherChildren = parents
        .Where(p => p.BiologicalSex == BiologicalSex.Female)
        .SelectMany(p => p.RelativeInfos)
        .Where(r => r.Type == RelationshipType.Child && r.Id != personInfo.Id)
        .ToArray();
    var allFatherChildren = parents
        .Where(p => p.BiologicalSex == BiologicalSex.Male)
        .SelectMany(p => p.RelativeInfos)
        .Where(r => r.Type == RelationshipType.Child && r.Id != personInfo.Id)
        .ToArray();
    var commonChildren = allFatherChildren.Intersect(allMotherChildren, _RelativeInfoComparer);
    var fatherChildren = allFatherChildren.Except(commonChildren, _RelativeInfoComparer);
    var motherChildren = allMotherChildren.Except(commonChildren, _RelativeInfoComparer);
    var adoptiveChildrenOfNativeParents = parents
      .SelectMany(p => p.RelativeInfos)
      .Where(r => r.Type == RelationshipType.AdoptiveChild);
    var childrenOfAdoptiveParents = adoptiveParents
        .SelectMany(i => i.Person.RelativeInfos.Select(r => r with { Date = i.Relative.Date }))
        .Where(r => r.Type == RelationshipType.Child || (r.Type == RelationshipType.AdoptiveChild && r.Id != personInfo.Id));
    var adoptiveChildren = ((IEnumerable<RelativeInfo>)[.. adoptiveChildrenOfNativeParents, .. childrenOfAdoptiveParents])
      .Distinct(_RelativeInfoComparer);
    var allParentIds = parents
      .Concat(adoptiveParents.Select(i => i.Person))
      .Select(p => p.Id)
      .ToHashSet();
    var stepParentTasks = parents
      .SelectMany(p => p.RelativeInfos)
      .Where(r => r.Type == RelationshipType.Spose)
      .Where(r => !allParentIds.Contains(r.Id))
      .Select(r => GetRelativePersonFullInfoAsync(r, token));
    var stepParents = await Task.WhenAll(stepParentTasks);
    var stepParentChildren = stepParents
      .SelectMany(i => i.Person.RelativeInfos.Select(r => r with { Date = i.Relative.Date }))
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Distinct(_RelativeInfoComparer);

    RelativeInfo[] ToTypedArray(IEnumerable<RelativeInfo> sibling, RelationshipType type) =>
      [.. sibling.Select(s => s with { Type = type })];

    return new Siblings(
      Native: ToTypedArray(commonChildren, RelationshipType.Sibling),
      ByMother: ToTypedArray(motherChildren, RelationshipType.SiblingByMother),
      ByFather: ToTypedArray(fatherChildren, RelationshipType.SiblingByFather),
      Adoptive: ToTypedArray(adoptiveChildren, RelationshipType.AdoptiveSibling),
      Step: ToTypedArray(stepParentChildren, RelationshipType.StepSibling));
  }

  public static RelativeInfo[] Parent(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.Parent)
    .ToArray();

  public static RelativeInfo[] Children(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.Child)
    .ToArray();

  public static RelativeInfo[] AdoptiveParent(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveParent)
    .ToArray();

  public static RelativeInfo[] AdoptiveChildren(PersonFullInfo info) =>
    info
    .RelativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveChild)
    .ToArray();

  public override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }
}
