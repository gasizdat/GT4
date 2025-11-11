using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class TableData : TableBase
{
  private static Data CreateData(SqliteDataReader reader)
  {
    var id = reader.GetInt32(0);
    var value = reader.GetStream(1);
    var mimeType = TryGetString(reader, 2);
    var category = GetEnum<DataCategory>(reader, 3);
    using var streamReader = new BinaryReader(value);
    var content = streamReader.ReadBytes((int)value.Length);
    var image = new Data(Id: id, Content: content, MimeType: mimeType, Category: category);

    return image;
  }

  public TableData(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
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
    var ret = (await reader.ReadAsync(token)) ? CreateData(reader) : null;

    return ret;
  }

  public async Task<Data> AddDataAsync(byte[] content, string? mimeType, DataCategory dataCategory, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Data (Value, MimeType, Category)
      VALUES (@value, @mimeType, @category);
      """;
    command.Parameters.AddWithValue("@value", content);
    command.Parameters.AddWithValue("@mimeType", mimeType is null ? DBNull.Value : mimeType);
    command.Parameters.AddWithValue("@category", (int)dataCategory);
    await command.ExecuteNonQueryAsync(token);

    return new Data(Id: await Document.GetLastInsertRowIdAsync(token), Content: content, MimeType: mimeType, Category: dataCategory);
  }

  public async Task RemoveDataAsync(Data data, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      DELETE FROM Data
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", data.Id);
    await command.ExecuteNonQueryAsync(token);
  }
}
