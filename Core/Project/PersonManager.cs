using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public class PersonManager : TableBase
{
  public PersonManager(ProjectDocument document)
    : base(document: document)
  {
  }

  private async Task<PersonInfo> CreatePersonInfoAsync(Person person, CancellationToken token)
  {
    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var mainPhoto = Document.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonMainPhoto, token);
    await Task.WhenAll(names, mainPhoto);
    var ret = new PersonInfo
    (
      person: person,
      names: names.Result,
      mainPhoto: mainPhoto.Result.FirstOrDefault()
    );

    return ret;
  }

  public async Task<PersonFullInfo> GetPersonFullInfoAsync(Person person, CancellationToken token)
  {
    if (person.Id == 0)
    {
      throw new ArgumentException("person.Id == 0");
    }

    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var personData = Document.PersonData.GetPersonDataSetAsync(person, null, token);
    var relatives = Document.Relatives.GetRelativeAsync(person, token);
    await Task.WhenAll(names, personData, relatives);

    var personInfo = await CreatePersonInfoAsync(person, token);
    var ret = new PersonFullInfo(
      Id: person.Id,
      BirthDate: person.BirthDate,
      DeathDate: person.DeathDate,
      BiologicalSex: person.BiologicalSex,
      Names: names.Result.ToArray(),
      MainPhoto: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonMainPhoto),
      AdditionalPhotos: personData.Result.Where(data => data.Category == DataCategory.PersonPhoto).ToArray(),
      Relatives: relatives.Result.ToArray(),
      Biography: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonBio));

    return ret;
  }

  public async Task<PersonInfo[]> GetPersonInfosByNameAsync(Name name, CancellationToken token)
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
      .Select(person => CreatePersonInfoAsync(person!, token)));

    return ret ?? [];
  }

  public async Task UpdatePersonInfoAsync(PersonInfo personInfo, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    await Document.PersonNames.UpdatePersonNamesAsync(personInfo, personInfo.Names, token);
    await Document.PersonData.UpdatePersonDataAsync(personInfo, personInfo.MainPhoto, DataCategory.PersonMainPhoto, token);

    transaction.Commit();
  }

  public async Task<PersonInfo> AddPersonInfoAsync(PersonFullInfo personFullInfo, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    var familyName = personFullInfo.Names.SingleOrDefault(name => name.Type == NameType.FamilyName);
    if (familyName is not null)
    {
      var requiredFamilyNames = await Document.FamilyManager.GetRequiredNames(familyName, personFullInfo, token);
      personFullInfo = personFullInfo with { Names = personFullInfo.Names.Concat(requiredFamilyNames).ToArray() };
    }

    var person = await Document.Persons.AddPersonAsync(personFullInfo, token);
    await Document.PersonNames.AddPersonNamesAsync(personFullInfo, personFullInfo.Names, token);

    transaction.Commit();

    return personFullInfo with { Id = person.Id };
  }

  public async Task UpdatePersonAsync(PersonFullInfo personFullInfo, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    await Document.Persons.UpdatePersonAsync(personFullInfo, token);
    await Document.PersonNames.UpdatePersonNamesAsync(personFullInfo, personFullInfo.Names, token);
    await Document.PersonData.UpdatePersonDataAsync(personFullInfo, personFullInfo.MainPhoto, DataCategory.PersonMainPhoto, token);

    transaction.Commit();
  }

  public override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }
}
