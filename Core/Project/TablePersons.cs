using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using System.Data.Common;

namespace GT4.Core.Project;

internal partial class TablePersons : TableBase, ITablePersons
{
  private readonly WeakReference<Dictionary<int, Person>?> _Items = new(null);

  public TablePersons(IProjectConnection connection) : base(connection)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Connection.CreateCommand();
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

  private static Person CreatePerson(DbDataReader reader)
  {
    var person = new Person
    (
      Id: reader.GetInt32(0),
      BirthDate: GetDate(reader, 1, 2),
      DeathDate: TryGetDate(reader, 3, 4),
      BiologicalSex: GetEnum<BiologicalSex>(reader, 5)
    );

    return person;
  }

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public async Task<Person[]> GetPersonsAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return [.. items.Values];

    using var command = Connection.CreateCommand();

    command.CommandText = """
      SELECT Id, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons;
      """;

    await using var reader = await command.ExecuteReaderAsync(token);
    var result = new Dictionary<int, Person>();
    while (await reader.ReadAsync(token))
    {
      var person = CreatePerson(reader);
      result.Add(person.Id, person);
    }

    _Items.SetTarget(result);
    return [.. result.Values];
  }

  public async Task<Person?> TryGetPersonByIdAsync(int personId, CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items.TryGetValue(personId, out var cached) ? cached : null;

    using var command = Connection.CreateCommand();

    command.CommandText = """
      SELECT Id, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", personId);

    await using var reader = await command.ExecuteReaderAsync(token);
    return (await reader.ReadAsync(token)) ? CreatePerson(reader) : null;
  }

  public async Task<Person> AddPersonAsync(Person person, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
    command.CommandText = """
      INSERT INTO Persons (BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex)
      VALUES (@birthDate, @birthDateStatus, @deathDate, @deathDateStatus, @biologicalSex)
      RETURNING Id;
      """;
    command.Parameters.AddWithValue("@birthDate", person.BirthDate.Code);
    command.Parameters.AddWithValue("@birthDateStatus", person.BirthDate.Status);
    command.Parameters.AddWithValue("@deathDate", person.DeathDate.HasValue ? person.DeathDate.Value.Code : DBNull.Value);
    command.Parameters.AddWithValue("@deathDateStatus", person.DeathDate.HasValue ? person.DeathDate.Value.Status : DBNull.Value);
    command.Parameters.AddWithValue("@biologicalSex", person.BiologicalSex);
    var insertedId = await command.ExecuteScalarAsync(token);
    var personId = Convert.ToInt32(insertedId);
    await transaction.CommitAsync(token);

    InvalidateItems();

    return person with { Id = personId };
  }

  public async Task UpdatePersonAsync(Person person, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
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
    await transaction.CommitAsync(token);

    InvalidateItems();
  }

  public async Task RemovePersonAsync(Person person, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
    command.CommandText = """
      DELETE FROM Persons
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", person.Id);
    await command.ExecuteNonQueryAsync(token);
    await transaction.CommitAsync(token);

    InvalidateItems();
  }
}
