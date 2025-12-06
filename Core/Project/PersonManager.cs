using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class PersonManager : TableBase, IPersonManager
{
  public PersonManager(IProjectDocument document)
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

    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var personData = Document.PersonData.GetPersonDataSetAsync(person, null, token);
    var relativeInfos = Document.RelativesProvider.GetRelativeInfosAsync(person, selectMainPhoto: true, token);
    await Task.WhenAll(names, personData, relativeInfos);

    var ret = new PersonFullInfo(
      person: person,
      names: names.Result,
      mainPhoto: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonMainPhoto),
      additionalPhotos: personData.Result.Where(data => data.Category == DataCategory.PersonPhoto).ToArray(),
      relativeInfos: relativeInfos.Result,
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

  internal override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }
}
