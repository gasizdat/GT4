using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal partial class TablePersonNames : TableBase, ITablePersonNames
{
  public TablePersonNames(IProjectDocument document) : base(document)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
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
      WHERE PersonId=@personId
      ORDER BY ROWID;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);

    await using var reader = await command.ExecuteReaderAsync(token);
    var tasks = new List<Task<Name?>>();
    while (await reader.ReadAsync(token))
    {
      var id = reader.GetInt32(0);
      tasks.Add(Document.Names.TryGetNameByIdAsync(id, token));
    }

    var names = await Task.WhenAll(tasks);
    return names
      .Where(name => name is not null)
      .Select(name => name!)
      .ToArray();
  }

  public async Task AddPersonNamesAsync(Person person, Name[] names, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    // Add person name one at a time to preserve the name order.
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

  public async Task UpdatePersonNamesAsync(Person person, Name[] names, CancellationToken token)
  {
    var oldNames = await GetPersonNamesAsync(person, token);

    using var transaction = await Document.BeginTransactionAsync(token);
    {
      using var command = Document.CreateCommand();

      command.CommandText = """
        DELETE FROM PersonNames
        WHERE PersonId=@personId;
        """;
      command.Parameters.AddWithValue("@personId", person.Id);

      await command.ExecuteNonQueryAsync(token);
    }

    await AddPersonNamesAsync(person, names, token);

    transaction.Commit();
  }
}
