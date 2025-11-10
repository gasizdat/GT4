using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class TableData : TableBase
{
  private readonly WeakReference<IReadOnlyDictionary<int, Data>?> _Items = new(null);

  private static Data CreateData(SqliteDataReader reader)
  {
    var id = reader.GetInt32(0);
    var value = reader.GetStream(1);
    var mimeType = reader.GetString(2);
    using var streamReader = new BinaryReader(value);
    var content = streamReader.ReadBytes((int)value.Length);
    var image = new Data(Id: id, Content: content, MimeType: mimeType);

    return image;
  }

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
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
          Value BLOB NOT NULL
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<IReadOnlyDictionary<int, Data>> GetDataAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, MimeType
      FROM Data;
      """;

    await using var reader = await command.ExecuteReaderAsync(token);
    var result = new Dictionary<int, Data>();
    while (await reader.ReadAsync(token))
    {
      var data = CreateData(reader);
      result.Add(data.Id, data);
    }

    _Items.SetTarget(result);
    return result;
  }

  public async Task<Data?> TryGetDataAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }
    if (_Items.TryGetTarget(out var items) && items.TryGetValue(id.Value, out var data))
    {
      return data;
    }
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, MimeType
      FROM Data
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", id.Value);

    await using var reader = await command.ExecuteReaderAsync(token);
    if (await reader.ReadAsync(token))
    {
      data = CreateData(reader);
      return data;
    }

    return null;
  }

  public async Task<Data> AddDataAsync(byte[] content, string mimeType, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Data (Value, MimeType)
      VALUES (@value, @mimeType);
      """;
    command.Parameters.AddWithValue("@value", content);
    command.Parameters.AddWithValue("@mimeType", mimeType);
    await command.ExecuteNonQueryAsync(token);
    InvalidateItems();

    return new Data(Id: await Document.GetLastInsertRowIdAsync(token), Content: content, MimeType: mimeType);
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
    InvalidateItems();
  }
}
