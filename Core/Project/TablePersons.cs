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
    return new Person
    (
      Id: personId,
      Names: await Document.PersonNames.GetPersonNamesAsync(personId, token),
      MainPhoto: (await Document.Data.GetDataAsync(TryGetInteger(reader, 1), token))?.Content,
      BirthDate: TryGetDateTime(reader, 2),
      BirthDateStatus: GetEnum<DateStatus>(reader, 3),
      DeathDate: TryGetDateTime(reader, 4),
      DeathDateStatus: TryGetEnum<DateStatus>(reader, 5),
      BiologicalSex: GetEnum<BiologicalSex>(reader, 6)
    );
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

    using var reader = await command.ExecuteReaderAsync(token);
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
    using var reader = await command.ExecuteReaderAsync(token);

    var result = new List<Person>();
    while (await reader.ReadAsync(token))
    {
      var person = await CreatePersonAsync(reader, token);
      result.Add(person);
    }
    return result.ToArray();
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
    command.Parameters.AddWithValue("@birthDate", person.BirthDate is not null ? person.BirthDate : DBNull.Value);
    command.Parameters.AddWithValue("@birthDateStatus", person.BirthDateStatus);
    command.Parameters.AddWithValue("@deathDate", person.DeathDate is not null ? person.DeathDate : DBNull.Value);
    command.Parameters.AddWithValue("@deathDateStatus", person.DeathDateStatus.HasValue ? (int)person.DeathDateStatus.Value : DBNull.Value);
    command.Parameters.AddWithValue("@biologicalSex", person.BiologicalSex);
    await command.ExecuteNonQueryAsync(token);
    var personId = await Document.GetLastInsertRowIdAsync(token);
    if (person.Names.Length > 0)
    {
      await Document.PersonNames.AddNamesAsync(personId, person.Names, token);
    }
    return personId;
  }
}
