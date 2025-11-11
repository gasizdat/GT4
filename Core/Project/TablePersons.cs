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
      return items.ToArray();

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
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

  public async Task<Person?> TryGetPersonByIdAsync(int personId, CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items.SingleOrDefault(item => item.Id == personId);

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
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

  public async Task<Person> AddPersonAsync(Person person, CancellationToken token)
  {
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
    
    InvalidateItems();

    return person with { Id = personId };
  }

  public async Task UpdatePersonAsync(Person person, CancellationToken token)
  {
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

    InvalidateItems();
  }
}
