using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using System.Data.Common;

namespace GT4.Core.Project;

internal class TableData : TableBase, ITableData
{
  private static Data CreateData(DbDataReader reader)
  {
    var id = reader.GetInt32(0);
    var content = reader.GetFieldValue<byte[]>(1);
    var mimeType = TryGetString(reader, 2);
    var category = GetEnum<DataCategory>(reader, 3);

    return new Data(Id: id, Content: content, MimeType: mimeType, Category: category);
  }

  public TableData(IProjectDocument document) : base(document)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Data (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Value BLOB NOT NULL,
          MimeType TEXT,
          Category INTEGER NOT NULL
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Data?> TryGetDataByIdAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, MimeType, Category
      FROM Data
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", id.Value);

    await using var reader = await command.ExecuteReaderAsync(token);
    return (await reader.ReadAsync(token)) ? CreateData(reader) : null;
  }

  public async Task<Data> AddDataAsync(byte[] content, string? mimeType, DataCategory dataCategory, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Data (Value, MimeType, Category)
      VALUES (@value, @mimeType, @category);
      """;
    command.Parameters.AddWithValue("@value", content);
    command.Parameters.AddWithValue("@mimeType", mimeType is null ? DBNull.Value : mimeType);
    command.Parameters.AddWithValue("@category", (int)dataCategory);
    await command.ExecuteNonQueryAsync(token);

    var ret = new Data(Id: await Document.GetLastInsertRowIdAsync(token), Content: content, MimeType: mimeType, Category: dataCategory);
    transaction.Commit();

    return ret;
  }

  public async Task RemoveDataAsync(Data data, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      DELETE FROM Data
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", data.Id);
    await command.ExecuteNonQueryAsync(token);
    transaction.Commit();
  }

  public async Task UpdateCategoryAsync(Data data, DataCategory dataCategory, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      UPDATE Data
      SET Category=@category
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", data.Id);
    command.Parameters.AddWithValue("@category", (int)dataCategory);
    await command.ExecuteNonQueryAsync(token);
    transaction.Commit();
  }
}
