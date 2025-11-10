using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public partial class TablePersonNames : TableBase
{
  public TablePersonNames(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS PersonNames (
          PersonId INTEGER NOT NULL,
          NameId INTEGER NOT NULL,
          FOREIGN KEY(PersonId) REFERENCES Persons(Id),
          FOREIGN KEY(NameId) REFERENCES Names(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Name[]> GetPersonNamesAsync(Person person, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT NameId
      FROM PersonNames
      WHERE PersonId=@personId;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);

    await using var reader = await command.ExecuteReaderAsync(token);
    var result = new List<Name>();
    while (await reader.ReadAsync(token))
    {
      var id = reader.GetInt32(0);
      var name = await Document.Names.GetNameAsync(id, token);
      if (name is not null)
      {
        result.Add(name);
      }
    }

    return result.ToArray();
  }

  public async Task AddNamesAsync(Person person, Name[] names, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    foreach (var name in names)
    {
      using var command = Document.CreateCommand();
      command.CommandText = """
        INSERT INTO PersonNames (PersonId, NameId)
        VALUES (@personId, @nameId);
        """;

      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@nameId", name.Id);
      await command.ExecuteNonQueryAsync(token);
    }

    transaction.Commit();
  }

  public async Task UpdateNamesAsync(Person person, Name[] names, CancellationToken token)
  {
    var oldNames = await GetPersonNamesAsync(person, token);
    var newNameIds = names.Select(n => n.Id).ToHashSet();
    var remainedNames = new HashSet<int>();
    var tasks = new List<Task>();

    using var transaction = await Document.BeginTransactionAsync(token);

    foreach (var oldName in oldNames)
    {
      var isNameRemained = newNameIds.Contains(oldName.Id) || oldName.Type == NameType.FamilyName; // Preserve Family Name
      if (isNameRemained)
      {
        remainedNames.Add(oldName.Id);
        continue;
      }

      using var command = Document.CreateCommand();

      command.CommandText = """
        DELETE FROM PersonNames
        WHERE PersonId=@personId AND NameId=@nameId;
        """;
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@nameId", oldName.Id);
      tasks.Add(command.ExecuteNonQueryAsync(token));
    }

    tasks.Add(AddNamesAsync(person, names.Where(n => !remainedNames.Contains(n.Id)).ToArray(), token));

    await Task.WhenAll(tasks);

    transaction.Commit();
  }
}
