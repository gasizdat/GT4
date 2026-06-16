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
          FOREIGN KEY(PersonId) REFERENCES Persons(Id) ON DELETE CASCADE,
          FOREIGN KEY(NameId) REFERENCES Names(Id) ON DELETE CASCADE
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Name[]> GetPersonNamesAsync(Person person, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT n.Id, n.Value, n.Type, n.ParentId
      FROM Names n
      INNER JOIN PersonNames pn ON pn.NameId = n.Id
      WHERE pn.PersonId = @personId
      ORDER BY pn.ROWID;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);

    var names = new List<Name>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      names.Add(new Name(
        Id: reader.GetInt32(0),
        Value: reader.GetString(1),
        Type: GetEnum<NameType>(reader, 2),
        ParentId: TryGetInteger(reader, 3)));
    }

    return names.ToArray();
  }

  public async Task<Dictionary<int, Name[]>> GetPersonNamesAsync(int[] personIds, CancellationToken token)
  {
    if (personIds.Length == 0)
      return [];

    var inClause = string.Join(",", personIds);
    using var command = Document.CreateCommand();
    command.CommandText = $"""
      SELECT pn.PersonId, n.Id, n.Value, n.Type, n.ParentId
      FROM Names n
      INNER JOIN PersonNames pn ON pn.NameId = n.Id
      WHERE pn.PersonId IN ({inClause})
      ORDER BY pn.PersonId, pn.ROWID;
      """;

    var buckets = new Dictionary<int, List<Name>>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      var personId = reader.GetInt32(0);
      if (!buckets.TryGetValue(personId, out var list))
        buckets[personId] = list = [];
      list.Add(new Name(
        Id: reader.GetInt32(1),
        Value: reader.GetString(2),
        Type: GetEnum<NameType>(reader, 3),
        ParentId: TryGetInteger(reader, 4)));
    }

    return buckets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
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
