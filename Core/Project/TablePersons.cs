using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public partial class TablePersons : TableBase
{
  private readonly WeakReference<IList<Person>?> _Items = new(null);

  public TablePersons(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Persons (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        MainPhotoId INTEGER,
        BirthDate NUMERIC,
        BirthDateStatus INTEGER NOT NULL,
        DeathDate NUMERIC,
        DeathDateStatus INTEGER,
        BiologicalSex INTEGER NOT NULL,
        FOREIGN KEY(MainPhotoId) REFERENCES Data(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  private async Task<Person> CreatePersonAsync(SqliteDataReader reader, CancellationToken token)
  {
    var personId = reader.GetInt32(0);
    var person = new Person
    (
      Id: personId,
      Names: [],
      MainPhoto: null,
      BirthDate: GetDate(reader, 2, 3),
      DeathDate: TryGetDate(reader, 4, 5),
      BiologicalSex: GetEnum<BiologicalSex>(reader, 6)
    );
    var names = Document.PersonNames.GetPersonNamesAsync(person, token);
    var mainPhoto = Document.Data.TryGetDataAsync(TryGetInteger(reader, 1), token);
    await Task.WhenAll(names, mainPhoto);

    return person with { Names = names.Result, MainPhoto = mainPhoto.Result?.Content };
  }

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public async Task<Person[]> GetPersonsAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items.ToArray();

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, MainPhotoId, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons;
      """;

    await using var reader = await command.ExecuteReaderAsync(token);
    var result = new List<Person>();
    while (await reader.ReadAsync(token))
    {
      var person = await CreatePersonAsync(reader, token);
      result.Add(person);
    }

    _Items.SetTarget(result);
    return result.ToArray();
  }

  public async Task<Person[]> GetPersonsByNameAsync(Name name, CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items.Where(p => p.Names.Count(n => n.Id == name.Id) > 0).ToArray();

    using var command = Document.CreateCommand();
    command.CommandText = """
      SELECT Id, MainPhotoId, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons
      INNER JOIN
        PersonNames ON PersonNames.PersonId=Persons.Id
      WHERE PersonNames.NameId=@id;
      """;
    command.Parameters.AddWithValue("@id", name.Id);
    await using var reader = await command.ExecuteReaderAsync(token);

    var result = new List<Person>();
    while (await reader.ReadAsync(token))
    {
      var person = await CreatePersonAsync(reader, token);
      result.Add(person);
    }
    return result.ToArray();
  }

  public async Task<Person?> TryGetPersonByIdAsync(int personId, CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items.SingleOrDefault(item => item.Id == personId);

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, MainPhotoId, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", personId);

    await using var reader = await command.ExecuteReaderAsync(token);
    if (await reader.ReadAsync(token))
    {
      return await CreatePersonAsync(reader, token);
    }

    return null;
  }

  public async Task<int> AddPersonAsync(Person person, CancellationToken token)
  {
    InvalidateItems();
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Persons (BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex)
      VALUES (@birthDate, @birthDateStatus, @deathDate, @deathDateStatus, @biologicalSex);
      """;
    command.Parameters.AddWithValue("@birthDate", person.BirthDate.Code);
    command.Parameters.AddWithValue("@birthDateStatus", person.BirthDate.Status);
    command.Parameters.AddWithValue("@deathDate", person.DeathDate.HasValue ? person.DeathDate.Value.Code : DBNull.Value);
    command.Parameters.AddWithValue("@deathDateStatus", person.DeathDate.HasValue ? person.DeathDate.Value.Status : DBNull.Value);
    command.Parameters.AddWithValue("@biologicalSex", person.BiologicalSex);
    await command.ExecuteNonQueryAsync(token);
    var personId = await Document.GetLastInsertRowIdAsync(token);
    if (person.Names.Length > 0)
    {
      await Document.PersonNames.AddNamesAsync(person, person.Names, token);
    }
    transaction.Commit();

    return personId;
  }

  public async Task UpdatePersonAsync(Person person, CancellationToken token)
  {
    InvalidateItems();
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      UPDATE Persons 
      SET BirthDate=@birthDate, BirthDateStatus=@birthDateStatus, DeathDate=@deathDate, DeathDateStatus=@deathDateStatus, BiologicalSex=@biologicalSex
      WHERE Id=@personId;
      """;
    command.Parameters.AddWithValue("@birthDate", person.BirthDate.Code);
    command.Parameters.AddWithValue("@birthDateStatus", person.BirthDate.Status);
    command.Parameters.AddWithValue("@deathDate", person.DeathDate.HasValue ? person.DeathDate.Value.Code : DBNull.Value);
    command.Parameters.AddWithValue("@deathDateStatus", person.DeathDate.HasValue ? person.DeathDate.Value.Status : DBNull.Value);
    command.Parameters.AddWithValue("@biologicalSex", person.BiologicalSex);
    command.Parameters.AddWithValue("@personId", person.Id);
    await command.ExecuteNonQueryAsync(token);

    if (person.Names.Length > 0)
    {
      await Document.PersonNames.UpdateNamesAsync(person, person.Names, token);
    }

    transaction.Commit();
  }
}
