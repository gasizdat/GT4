using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class TableRelatives : TableBase
{
  private async Task<Relative?> CreateRelativeAsync(SqliteDataReader reader, CancellationToken token)
  {
    var id = reader.GetInt32(0);
    var type = GetEnum<RelationshipType>(reader, 1);
    var date = TryGetDate(reader, 2, 3);
    var relative = await Document.Persons.TryGetPersonById(id, token);

    return relative is null ? null : new Relative(Person: relative, Type: type, Date: date);
  }

  public TableRelatives(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Relatives (
          PersonId INTEGER NOT NULL,
          RelativeId INTEGER NOT NULL,
          Type INTEGER NOT NULL,
          Date INTEGER,
          DateStatus INTEGER,
          FOREIGN KEY(PersonId) REFERENCES Persons(Id),
          FOREIGN KEY(RelativeId) REFERENCES Persons(Id),
      	  PRIMARY KEY (PersonId, RelativeId, Type, Date)
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Relative[]> GetRelativeAsync(int personId, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT RelativeId, Type, Date, DateStatus
      FROM Relatives
      WHERE PersonId=@id;
      """;
    command.Parameters.AddWithValue("@id", personId);

    await using var reader = await command.ExecuteReaderAsync(token);
    var tasks = new List<Task<Relative?>>();
    while (await reader.ReadAsync(token))
    {
      tasks.Add(CreateRelativeAsync(reader, token));
    }

    return (await Task.WhenAll(tasks))
      .Where(i => i is not null)
      .Select(i => i!)
      .ToArray() ?? [];
  }

  public async Task<Relative> AddRelativeAsync(Person person, Person relative, RelationshipType type, Date? date, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Parents (PersonId, RelativeId, Type, Date, DateStatus)
      VALUES (@personId, @relativeId, @type, @date, @dateStatus);
      """;
    command.Parameters.AddWithValue("@personId", person.Id);
    command.Parameters.AddWithValue("@relativeId", relative.Id);
    command.Parameters.AddWithValue("@type", type);
    command.Parameters.AddWithValue("@date", date.HasValue ? date.Value.Code : DBNull.Value);
    command.Parameters.AddWithValue("@dateStatus", date.HasValue ? date.Value.Status : DBNull.Value);
    await command.ExecuteNonQueryAsync(token);

    return new Relative(Person: relative, Type: type, Date: date);
  }
}
