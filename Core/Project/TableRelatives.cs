using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class TableRelatives : TableBase
{
  private static RelationshipType GetBackwardDirection(RelationshipType relationshipType)
  {
    var ret = relationshipType switch
    {
      RelationshipType.Child => RelationshipType.Parent,
      RelationshipType.AdoptiveChild => RelationshipType.AdoptiveParent,
      RelationshipType.Parent => RelationshipType.Child,
      RelationshipType.AdoptiveParent => RelationshipType.AdoptiveChild,
      _ => relationshipType
    };

    return ret;
  }

  private static bool IsBackwardDirection(Relative relative)
  {
    return relative.Type switch
    {
      RelationshipType.Child => true,
      RelationshipType.AdoptiveChild => true,
      _ => false
    };
  }

  private static void AddCommandParameters(Person person, Relative relative, SqliteCommand command)
  {
    switch (relative.Type)
    {
      case RelationshipType.Parent:
      case RelationshipType.Child:
      case RelationshipType.Spose:
      case RelationshipType.AdoptiveParent:
      case RelationshipType.AdoptiveChild:
        break;
      default:
        throw new ArgumentException(nameof(relative.Type));
    }

    if (IsBackwardDirection(relative))
    {
      command.Parameters.AddWithValue("@personId", relative.Id);
      command.Parameters.AddWithValue("@relativeId", person.Id);
      command.Parameters.AddWithValue("@type", GetBackwardDirection(relative.Type));
    }
    else
    {
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@relativeId", relative.Id);
      command.Parameters.AddWithValue("@type", relative.Type);
    }
  }

  private async Task<Relative?> CreateRelativeAsync(SqliteDataReader reader, bool forwardLink, CancellationToken token)
  {
    var id = reader.GetInt32(0);
    var type = GetEnum<RelationshipType>(reader, 1);
    var date = TryGetDate(reader, 2, 3);
    var relative = await Document.Persons.TryGetPersonByIdAsync(id, token);
    if (forwardLink == false)
    {
      type = GetBackwardDirection(type);
    }

    return relative is null ? null : new Relative(relative, type, date);
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
    var tasks = new List<Task<Relative?>>();

    using (var command = Document.CreateCommand())
    {
      command.CommandText = """
        SELECT RelativeId, Type, Date, DateStatus
        FROM Relatives
        WHERE PersonId=@id;
      """;
      command.Parameters.AddWithValue("@id", person.Id);

      await using var reader = await command.ExecuteReaderAsync(token);
      while (await reader.ReadAsync(token))
      {
        tasks.Add(CreateRelativeAsync(reader, forwardLink: true, token));
      }
    }

    using (var command = Document.CreateCommand())
    {
      command.CommandText = """
        SELECT PersonId, Type, Date, DateStatus
        FROM Relatives
        WHERE RelativeId=@id;
      """;
      command.Parameters.AddWithValue("@id", person.Id);

      await using var reader = await command.ExecuteReaderAsync(token);
      while (await reader.ReadAsync(token))
      {
        tasks.Add(CreateRelativeAsync(reader, forwardLink: false, token));
      }
    }

    return (await Task.WhenAll(tasks))
      .Where(i => i is not null)
      .Select(i => i!)
      .ToArray();
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
      AddCommandParameters(person, relative, command);
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
    var newRelatives = relatives.ToDictionary(r => r.Id, r => r);
    var remainedRelatives = new HashSet<int>();
    var tasks = new List<Task>();

    using var transaction = await Document.BeginTransactionAsync(token);

    foreach (var oldRelative in oldRelatives)
    {
      if (newRelatives.TryGetValue(oldRelative.Id, out var newRelative) && newRelative.Date == oldRelative.Date)
      {
        remainedRelatives.Add(oldRelative.Id);
        continue;
      }

      using var command = Document.CreateCommand();

      command.CommandText = """
        DELETE FROM Relatives
        WHERE PersonId=@personId AND RelativeId=@relativeId AND Type=@type;
        """;
      AddCommandParameters(person, oldRelative, command);
      tasks.Add(command.ExecuteNonQueryAsync(token));
    }

    tasks.Add(AddRelativesAsync(person, relatives.Where(r => !remainedRelatives.Contains(r.Id)).ToArray(), token));
    await Task.WhenAll(tasks);

    transaction.Commit();
  }
}
