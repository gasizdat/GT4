using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.Core.Project;

internal class PersonManager : ProjectComponentBase, IPersonManager
{
  public PersonManager(IProjectDocument document)
    : base(document: document)
  {
  }

  private static Data[] CombinePersonData(PersonFullInfo personFullInfo)
  {
    var personDataSet = new List<Data>();

    if (personFullInfo.MainPhoto is not null)
    {
      personDataSet.Add(personFullInfo.MainPhoto);
    }

    personDataSet.AddRange(personFullInfo.AdditionalPhotos);

    if (personFullInfo.Biography is not null)
    {
      personDataSet.Add(personFullInfo.Biography);
    }

    if (personFullInfo.GedcomData is not null)
    {
      personDataSet.Add(personFullInfo.GedcomData);
    }

    return personDataSet.ToArray();
  }

  public async Task<PersonFullInfo> GetPersonFullInfoAsync(Person person, CancellationToken token)
  {
    if (person.Id == ElementId.NonCommittedId)
    {
      throw new ArgumentException("person is not committed");
    }

    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var personData = Document.PersonData.GetPersonDataSetAsync(person, null, token);
    var relativeInfos = Document.RelativesProvider.GetRelativeInfosAsync(person, selectMainPhoto: true, token);
    await Task.WhenAll(names, personData, relativeInfos);

    var ret = new PersonFullInfo(
      person: person,
      names: names.Result,
      mainPhoto: personData.Result.SingleOrDefault(data => data.Category.IsMainPhoto()),
      additionalPhotos: personData.Result.Where(data => data.Category.IsAdditionalPhoto()).ToArray(),
      relativeInfos: relativeInfos.Result,
      biography: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonBio),
      gedcomData: personData.Result.SingleOrDefault(data => data.Category == DataCategory.PersonGedcomTags));

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
    if (persons.Length == 0)
      return [];

    var namesTask = Document.PersonNames.GetPersonNamesAsync(persons, token);
    var photosTask = selectMainPhoto
      ? Document.PersonData.GetMergedPhotoSetAsync(persons, DataCategory.PersonMainPhoto, token)
      : Task.FromResult<Dictionary<int, Data[]>>([]);
    await Task.WhenAll(namesTask, photosTask);

    return persons.Select(person =>
    {
      namesTask.Result.TryGetValue(person.Id, out var names);
      photosTask.Result.TryGetValue(person.Id, out var photos);
      return new PersonInfo(person, names ?? [], photos?.FirstOrDefault());
    }).ToArray();
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

    var ids = new HashSet<int>();
    await using (var reader = await command.ExecuteReaderAsync(token))
    {
      while (await reader.ReadAsync(token))
      {
        ids.Add(reader.GetInt32(0));
      }
    }

    if (ids.Count == 0)
      return [];

    var allPersons = await Document.Persons.GetPersonsAsync(token);
    var persons = allPersons.Where(p => ids.Contains(p.Id)).ToArray();
    return await GetPersonInfosAsync(persons, selectMainPhoto, token);
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

    // Sequential: writes inside a transaction must take turns on the single connection.
    await Document.PersonNames.AddPersonNamesAsync(person, personFullInfo.Names, token);
    await Document.PersonData.AddPersonDataSetAsync(person, CombinePersonData(personFullInfo), token);
    await Document.Relatives.AddRelativesAsync(person, personFullInfo.RelativeInfos, token);

    await transaction.CommitAsync(token);

    return personFullInfo with { Id = person.Id };
  }

  public async Task UpdatePersonAsync(PersonFullInfo personFullInfo, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    // Re-buckets every photo's main/additional category to match personFullInfo.MainPhoto,
    // preserving each photo's tagged-vs-plain status.
    var mainPhotoId = personFullInfo.MainPhoto?.Id;
    if (mainPhotoId is not null)
    {
      var mainPhoto = await Document.Data.TryGetDataByIdAsync(mainPhotoId.Value, token);
      if (mainPhoto is not null)
      {
        if (!mainPhoto.Category.IsMainPhoto())
        {
          await Document.Data.UpdateCategoryAsync(mainPhoto, mainPhoto.Category.AsMainPhoto(), token);
          foreach (var photo in personFullInfo.AdditionalPhotos)
          {
            await Document.Data.UpdateCategoryAsync(photo, photo.Category.AsAdditionalPhoto(), token);
          }
        }
      }
    }

    // Sequential: writes inside a transaction must take turns on the single connection.
    await Document.Persons.UpdatePersonAsync(personFullInfo, token);
    await Document.PersonNames.UpdatePersonNamesAsync(personFullInfo, personFullInfo.Names, token);
    await Document.PersonData.UpdatePersonDataSetAsync(personFullInfo, CombinePersonData(personFullInfo), token);
    await Document.Relatives.UpdateRelativesAsync(personFullInfo, personFullInfo.RelativeInfos, token);

    await transaction.CommitAsync(token);
  }
}
