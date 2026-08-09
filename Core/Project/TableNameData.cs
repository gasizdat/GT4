using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace GT4.Core.Project;

internal class TableNameData : TableBase, ITableNameData
{
  private readonly ITableData _Data;

  public TableNameData(IProjectConnection connection, ITableData data) : base(connection) => _Data = data;

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Connection.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS NameData (
        NameId INTEGER NOT NULL,
        DataId INTEGER NOT NULL,
        FOREIGN KEY(NameId) REFERENCES Names(Id) ON DELETE CASCADE,
        -- DataId is intentionally NOT cascaded: deleting a name drops its data links (via NameId),
        -- but a Data blob still referenced by another owner (a name or a person) must NOT be deletable
        -- (RemoveNameDataAsync relies on this to detect "still in use").
        FOREIGN KEY(DataId) REFERENCES Data(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS NameDataCategory ON NameData(NameId, DataId);";
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Data[]> GetNameDataSetAsync(Name name, DataCategory? category, CancellationToken token)
  {
    using var command = Connection.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = """
        SELECT d.Id, d.Value, d.MimeType, d.Category
        FROM Data d
        INNER JOIN NameData nd ON nd.DataId = d.Id AND nd.NameId = @nameId
        WHERE d.Category = @category
        ORDER BY nd.ROWID;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = """
        SELECT d.Id, d.Value, d.MimeType, d.Category
        FROM Data d
        INNER JOIN NameData nd ON nd.DataId = d.Id
        WHERE nd.NameId = @nameId
        ORDER BY nd.ROWID;
        """;
    }
    command.Parameters.AddWithValue("@nameId", name.Id);

    var result = new List<Data>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      result.Add(CreateDataFromRow(reader));
    }

    return [.. result];
  }

  public async Task<Dictionary<int, Data[]>> GetNameDataSetAsync(Name[] names, DataCategory? category, CancellationToken token)
  {
    if (names.Length == 0)
      return [];

    var inClause = string.Join(",", names.Select(n => n.Id));
    using var command = Connection.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = $"""
        SELECT d.Id, d.Value, d.MimeType, d.Category, nd.NameId
        FROM Data d
        INNER JOIN NameData nd ON nd.DataId = d.Id AND nd.NameId IN ({inClause})
        WHERE d.Category = @category
        ORDER BY nd.NameId, nd.ROWID;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = $"""
        SELECT d.Id, d.Value, d.MimeType, d.Category, nd.NameId
        FROM Data d
        INNER JOIN NameData nd ON nd.DataId = d.Id
        WHERE nd.NameId IN ({inClause})
        ORDER BY nd.NameId, nd.ROWID;
        """;
    }

    var buckets = new Dictionary<int, List<Data>>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      var nameId = reader.GetInt32(4);
      if (!buckets.TryGetValue(nameId, out var list))
        buckets[nameId] = list = [];
      list.Add(CreateDataFromRow(reader));
    }

    return buckets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
  }

  public async Task AddNameDataSetAsync(Name name, Data[] dataSet, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);

    // Add name data one at a time to preserve the data order.
    foreach (var data in dataSet)
    {
      var dataId = (await AddDataContentIfNotExist(data, token)).Id;
      using var command = Connection.CreateCommand();

      command.CommandText = """
        INSERT INTO NameData (NameId, DataId)
        VALUES (@nameId, @dataId);
        """;
      command.Parameters.AddWithValue("@nameId", name.Id);
      command.Parameters.AddWithValue("@dataId", dataId);
      await command.ExecuteNonQueryAsync(token);
    }
    await transaction.CommitAsync(token);
  }

  public async Task UpdateNameDataSetAsync(Name name, Data[] dataSet, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
    command.CommandText = """
      DELETE FROM NameData
      WHERE NameId=@nameId;
      """;
    command.Parameters.AddWithValue("@nameId", name.Id);
    await command.ExecuteNonQueryAsync(token);

    await AddNameDataSetAsync(name, dataSet, token);

    await transaction.CommitAsync(token);
  }

  public async Task UpdateNameDataAsync(Name name, Data? newData, DataCategory dataCategory, CancellationToken token)
  {
    var resource = await GetNameDataSetAsync(name, dataCategory, token);
    if (resource.Count() > 1)
    {
      throw new ArgumentException($"The data with category '{dataCategory}' has multiple items");
    }

    var oldData = resource.FirstOrDefault();
    if (newData?.Id == oldData?.Id)
    {
      return;
    }

    using var transaction = await Connection.BeginTransactionAsync(token);
    if (newData is not null)
    {
      newData = await AddDataContentIfNotExist(newData, token);
    }

    if (oldData is not null)
    {
      await RemoveNameDataAsync(name, oldData, token);
    }

    if (newData is not null)
    {
      await AddNameDataSetAsync(name, [newData], token);
    }

    await transaction.CommitAsync(token);
  }

  public async Task RemoveNameDataAsync(Name name, Data data, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
    command.CommandText = """
      DELETE FROM NameData
      WHERE NameId=@nameId AND DataId=@dataId;
      """;
    command.Parameters.AddWithValue("@nameId", name.Id);
    command.Parameters.AddWithValue("@dataId", data.Id);
    await command.ExecuteNonQueryAsync(token);

    try
    {
      await _Data.RemoveDataAsync(data, token);
    }
    catch (SqliteException)
    {
      // The data content is still referenced by another owner (a name or a person): the foreign key
      // blocks its deletion, which is expected here. Any other failure is genuine and must surface.
    }

    await transaction.CommitAsync(token);
  }

  private static Data CreateDataFromRow(DbDataReader reader) =>
    new Data(
      Id: reader.GetInt32(0),
      Content: reader.GetFieldValue<byte[]>(1),
      MimeType: TryGetString(reader, 2),
      Category: GetEnum<DataCategory>(reader, 3));

  private async Task<Data> AddDataContentIfNotExist(Data data, CancellationToken token)
  {
    if (data.Id == ElementId.NonCommittedId)
    {
      return await _Data.AddDataAsync(data.Content, data.MimeType, data.Category, token);
    }

    return data;
  }
}
