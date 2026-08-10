using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace GT4.Core.Project;

internal abstract class TableOwnerData : TableBase
{
  private readonly ITableData _Data;
  private readonly string _TableName;
  private readonly string _OwnerTable;
  private readonly string _OwnerColumn;
  private readonly string _IndexName;

  protected TableOwnerData(
    IProjectConnection connection,
    ITableData data,
    string tableName,
    string ownerTable,
    string ownerColumn,
    string indexName) : base(connection)
  {
    _Data = data;
    _TableName = tableName;
    _OwnerTable = ownerTable;
    _OwnerColumn = ownerColumn;
    _IndexName = indexName;
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Connection.CreateCommand();
    command.CommandText = $"""
      CREATE TABLE IF NOT EXISTS {_TableName} (
        {_OwnerColumn} INTEGER NOT NULL,
        DataId INTEGER NOT NULL,
        FOREIGN KEY({_OwnerColumn}) REFERENCES {_OwnerTable}(Id) ON DELETE CASCADE,
        -- DataId is intentionally NOT cascaded: deleting the owner drops its data links (via
        -- {_OwnerColumn}), but a Data blob still referenced by another owner must NOT be deletable
        -- (RemoveDataAsync relies on this to detect "still in use").
        FOREIGN KEY(DataId) REFERENCES Data(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS {_IndexName} ON {_TableName}({_OwnerColumn}, DataId);";
    await command.ExecuteNonQueryAsync(token);
  }

  protected async Task<Data[]> GetDataSetAsync(int ownerId, DataCategory? category, CancellationToken token)
  {
    using var command = Connection.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = $"""
        SELECT d.Id, d.Value, d.MimeType, d.Category
        FROM Data d
        INNER JOIN {_TableName} od ON od.DataId = d.Id AND od.{_OwnerColumn} = @ownerId
        WHERE d.Category = @category
        ORDER BY od.ROWID;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = $"""
        SELECT d.Id, d.Value, d.MimeType, d.Category
        FROM Data d
        INNER JOIN {_TableName} od ON od.DataId = d.Id
        WHERE od.{_OwnerColumn} = @ownerId
        ORDER BY od.ROWID;
        """;
    }
    command.Parameters.AddWithValue("@ownerId", ownerId);

    var result = new List<Data>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      result.Add(CreateDataFromRow(reader));
    }

    return [.. result];
  }

  protected async Task<Dictionary<int, Data[]>> GetDataSetAsync(int[] ownerIds, DataCategory? category, CancellationToken token)
  {
    if (ownerIds.Length == 0)
      return [];

    var inClause = string.Join(",", ownerIds);
    using var command = Connection.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = $"""
        SELECT d.Id, d.Value, d.MimeType, d.Category, od.{_OwnerColumn}
        FROM Data d
        INNER JOIN {_TableName} od ON od.DataId = d.Id AND od.{_OwnerColumn} IN ({inClause})
        WHERE d.Category = @category
        ORDER BY od.{_OwnerColumn}, od.ROWID;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = $"""
        SELECT d.Id, d.Value, d.MimeType, d.Category, od.{_OwnerColumn}
        FROM Data d
        INNER JOIN {_TableName} od ON od.DataId = d.Id
        WHERE od.{_OwnerColumn} IN ({inClause})
        ORDER BY od.{_OwnerColumn}, od.ROWID;
        """;
    }

    var buckets = new Dictionary<int, List<Data>>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      var ownerId = reader.GetInt32(4);
      if (!buckets.TryGetValue(ownerId, out var list))
        buckets[ownerId] = list = [];
      list.Add(CreateDataFromRow(reader));
    }

    return buckets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
  }

  protected async Task AddDataSetAsync(int ownerId, Data[] dataSet, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);

    // Add data one at a time to preserve the data order.
    foreach (var data in dataSet)
    {
      var dataId = (await AddDataContentIfNotExist(data, token)).Id;
      using var command = Connection.CreateCommand();

      command.CommandText = $"""
        INSERT INTO {_TableName} ({_OwnerColumn}, DataId)
        VALUES (@ownerId, @dataId);
        """;
      command.Parameters.AddWithValue("@ownerId", ownerId);
      command.Parameters.AddWithValue("@dataId", dataId);
      await command.ExecuteNonQueryAsync(token);
    }
    await transaction.CommitAsync(token);
  }

  protected async Task UpdateDataSetAsync(int ownerId, Data[] dataSet, CancellationToken token)
  {
    var current = await GetDataSetAsync(ownerId, null, token);
    var removed = current.Where(d => !dataSet.Any(n => n.Id == d.Id));

    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
    command.CommandText = $"""
      DELETE FROM {_TableName}
      WHERE {_OwnerColumn}=@ownerId;
      """;
    command.Parameters.AddWithValue("@ownerId", ownerId);
    await command.ExecuteNonQueryAsync(token);

    await AddDataSetAsync(ownerId, dataSet, token);

    // Links are already gone (deleted above), so a removed blob's own link can no longer be the
    // reason RemoveDataAsync's FK check fails -- only a still-live link from another owner can.
    foreach (var data in removed)
    {
      try
      {
        await _Data.RemoveDataAsync(data, token);
      }
      catch (SqliteException)
      {
        // The data content is still referenced by another owner: the foreign key blocks its deletion,
        // which is expected here. Any other failure is genuine and must surface.
      }
    }

    await transaction.CommitAsync(token);
  }

  protected async Task UpdateDataAsync(int ownerId, Data? newData, DataCategory dataCategory, CancellationToken token)
  {
    var resource = await GetDataSetAsync(ownerId, dataCategory, token);
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
      await RemoveDataAsync(ownerId, oldData, token);
    }

    if (newData is not null)
    {
      await AddDataSetAsync(ownerId, [newData], token);
    }

    await transaction.CommitAsync(token);
  }

  protected async Task RemoveDataAsync(int ownerId, Data data, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = Connection.CreateCommand();
    command.CommandText = $"""
      DELETE FROM {_TableName}
      WHERE {_OwnerColumn}=@ownerId AND DataId=@dataId;
      """;
    command.Parameters.AddWithValue("@ownerId", ownerId);
    command.Parameters.AddWithValue("@dataId", data.Id);
    await command.ExecuteNonQueryAsync(token);

    try
    {
      await _Data.RemoveDataAsync(data, token);
    }
    catch (SqliteException)
    {
      // The data content is still referenced by another owner: the foreign key blocks its deletion,
      // which is expected here. Any other failure is genuine and must surface.
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
