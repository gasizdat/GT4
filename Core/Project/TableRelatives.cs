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
    var relative = await Document.Persons.TryGetPersonByIdAsync(id, token);

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

  public async Task<Relative[]> GetRelativesAsync(Person person, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT RelativeId, Type, Date, DateStatus
      FROM Relatives
      WHERE PersonId=@id;
      """;
    command.Parameters.AddWithValue("@id", person.Id);

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

  public async Task AddRelativesAsync(Person person, Relative[] relatives, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    var tasks = new List<Task>();

    foreach (var relative in relatives)
    {
      using var command = Document.CreateCommand();
      command.CommandText = """
        INSERT INTO Relatives (PersonId, RelativeId, Type, Date, DateStatus)
        VALUES (@personId, @relativeId, @type, @date, @dateStatus);
        """;
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@relativeId", relative.Person.Id);
      command.Parameters.AddWithValue("@type", relative.Type);
      command.Parameters.AddWithValue("@date", relative.Date.HasValue ? relative.Date.Value.Code : DBNull.Value);
      command.Parameters.AddWithValue("@dateStatus", relative.Date.HasValue ? relative.Date.Value.Status : DBNull.Value);
      tasks.Add(command.ExecuteNonQueryAsync(token));
    }

    await Task.WhenAll(tasks);
    transaction.Commit();
  }

  public async Task UpdateRelativesAsync(Person person, Relative[] relatives, CancellationToken token)
  {
    var oldRelatives = await GetRelativesAsync(person, token);
    var newRelativeIds = relatives.Select(r => r.Id).ToHashSet();
    var remainedRelatives = new HashSet<int>();
    var tasks = new List<Task>();

    using var transaction = await Document.BeginTransactionAsync(token);

    foreach (var oldRelative in oldRelatives)
    {
      var isRelativeRemained = newRelativeIds.Contains(oldRelative.Id);
      if (isRelativeRemained)
      {
        remainedRelatives.Add(oldRelative.Id);
        continue;
      }

      using var command = Document.CreateCommand();

      command.CommandText = """
        DELETE FROM Relatives
        WHERE PersonId=@personId AND RelativeId=@relativeId AND Type=@type;
        """;
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@relativeId", oldRelative.Id);
      command.Parameters.AddWithValue("@type", oldRelative.Type);
      tasks.Add(command.ExecuteNonQueryAsync(token));
    }

    tasks.Add(AddRelativesAsync(person, relatives.Where(r => !remainedRelatives.Contains(r.Id)).ToArray(), token));

    await Task.WhenAll(tasks);

    transaction.Commit();
  }
}
