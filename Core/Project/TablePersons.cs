using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Reflection.Metadata;

namespace GT4.Core.Project;

public partial class TablePersons : TableBase
{
  private readonly WeakReference<IReadOnlyDictionary<int, Person>?> _Items = new(null);

  public TablePersons(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Persons (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        BirthDate NUMERIC,
        BirthDateStatus INTEGER NOT NULL,
        DeathDate NUMERIC,
        DeathDateStatus INTEGER,
        BiologicalSex INTEGER NOT NULL
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
      BirthDate: TryGetDateTime(reader, 11),
      BirthDateStatus: GetEnum<DateStatus>(reader, 12),
      DeathDate: TryGetDateTime(reader, 13),
      DeathDateStatus: TryGetEnum<DateStatus>(reader, 14),
      BiologicalSex: GetEnum<BiologicalSex>(reader, 15)
    );
  }

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public async Task<IReadOnlyDictionary<int, Person>> GetPersonsAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons;
      """;

    using var reader = await command.ExecuteReaderAsync(token);
    var result = new Dictionary<int, Person>();
    while (await reader.ReadAsync(token))
    {
      var person = await CreatePersonAsync(reader, token);
      result.Add(person.Id, person);
    }

    _Items.SetTarget(result);
    return result;
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
