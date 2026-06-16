using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Collections.Concurrent;

namespace GT4.Core.Project;

internal class TableRelatives : TableBase, ITableRelatives
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

  private static void AddCommandParameters(Person person, Relative relative, ProjectCommand command)
  {
    switch (relative.Type)
    {
      case RelationshipType.Parent:
      case RelationshipType.Child:
      case RelationshipType.Spouse:
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

  private static Relative CreateRelativeFromRow(System.Data.Common.DbDataReader reader, bool forwardLink)
  {
    var id = reader.GetInt32(0);
    var type = GetEnum<RelationshipType>(reader, 1);
    var date = TryGetDate(reader, 2, 3);
    var birthDate = GetDate(reader, 4, 5);
    var deathDate = TryGetDate(reader, 6, 7);
    var biologicalSex = GetEnum<BiologicalSex>(reader, 8);
    var actualType = forwardLink ? type : GetBackwardDirection(type);
    return new Relative(new Person(id, birthDate, deathDate, biologicalSex), actualType, date);
  }

  public TableRelatives(IProjectDocument document) : base(document)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Relatives (
          PersonId INTEGER NOT NULL,
          RelativeId INTEGER NOT NULL,
          Type INTEGER NOT NULL,
          Date INTEGER,
          DateStatus INTEGER,
          FOREIGN KEY(PersonId) REFERENCES Persons(Id) ON DELETE CASCADE,
          FOREIGN KEY(RelativeId) REFERENCES Persons(Id) ON DELETE CASCADE,
      	  PRIMARY KEY (PersonId, RelativeId, Type, Date)
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Relative[]> GetRelativesAsync(Person person, CancellationToken token)
  {
    var relatives = new List<Relative>();

    using (var command = Document.CreateCommand())
    {
      command.CommandText = """
        SELECT r.RelativeId, r.Type, r.Date, r.DateStatus,
               p.BirthDate, p.BirthDateStatus, p.DeathDate, p.DeathDateStatus, p.BiologicalSex
        FROM Relatives r
        INNER JOIN Persons p ON p.Id = r.RelativeId
        WHERE r.PersonId = @id;
      """;
      command.Parameters.AddWithValue("@id", person.Id);

      await using var reader = await command.ExecuteReaderAsync(token);
      while (await reader.ReadAsync(token))
        relatives.Add(CreateRelativeFromRow(reader, forwardLink: true));
    }

    using (var command = Document.CreateCommand())
    {
      command.CommandText = """
        SELECT r.PersonId, r.Type, r.Date, r.DateStatus,
               p.BirthDate, p.BirthDateStatus, p.DeathDate, p.DeathDateStatus, p.BiologicalSex
        FROM Relatives r
        INNER JOIN Persons p ON p.Id = r.PersonId
        WHERE r.RelativeId = @id;
      """;
      command.Parameters.AddWithValue("@id", person.Id);

      await using var reader = await command.ExecuteReaderAsync(token);
      while (await reader.ReadAsync(token))
        relatives.Add(CreateRelativeFromRow(reader, forwardLink: false));
    }

    return relatives.ToArray();
  }

  public async Task AddRelativesAsync(Person person, Relative[] relatives, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    // Sequential: writes inside a transaction must take turns on the single connection.
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
      await command.ExecuteNonQueryAsync(token);
    }

    transaction.Commit();
  }

  public async Task UpdateRelativesAsync(Person person, Relative[] relatives, CancellationToken token)
  {
    var oldRelatives = await GetRelativesAsync(person, token);
    var newRelatives = relatives.ToDictionary(r => r.Id, r => r);
    var remainedRelatives = new HashSet<int>();

    using var transaction = await Document.BeginTransactionAsync(token);

    // Sequential: writes inside a transaction must take turns on the single connection.
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
      await command.ExecuteNonQueryAsync(token);
    }

    await AddRelativesAsync(person, relatives.Where(r => !remainedRelatives.Contains(r.Id)).ToArray(), token);

    transaction.Commit();
  }

  public async Task<bool> HasCommonAncestorsAsync(Person personA, Person personB, CancellationToken token)
  {
    async Task GetAllRelativesAsync(Person person, RelationshipType relationshipType, ConcurrentBag<Person> relatives)
    {
      relatives.Add(person);
      var personRelatives = await GetRelativesAsync(person, token);
      var parentTasks = personRelatives
        .Where(r => r.Type == relationshipType)
        .Select(p => GetAllRelativesAsync(p, relationshipType, relatives));

      await Task.WhenAll(parentTasks);
    }

    ConcurrentBag<Person> ancestorsA = new();
    ConcurrentBag<Person> ancestorsB = new();
    ElementIdComparer<Person> personComparer = new();

    await Task.WhenAll(
      GetAllRelativesAsync(personA, RelationshipType.Parent, ancestorsA),
      GetAllRelativesAsync(personB, RelationshipType.Parent, ancestorsB));
    var firstIntersection = ancestorsA
      .Intersect(ancestorsB, personComparer)
      .FirstOrDefault();

    return firstIntersection != null;
  }
}
