using Microsoft.Data.Sqlite;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public class TableNames : TableBase
{
  private record NamePlaceholder(int Id) : Name(Id: Id, Value: string.Empty, Type: NameType.MaleName, null);
  private readonly WeakReference<IReadOnlyDictionary<int, Name>?> _Items = new(null);

  private static Name? GetNamePlaceholder(int? id) =>
    id == null ? null : new NamePlaceholder(id.Value);

  private static Name CreateName(SqliteDataReader reader)
  {
    var id = reader.GetInt32(0);
    var value = reader.GetString(1);
    var type = (NameType)reader.GetInt32(2);
    var parentId = TryGetInteger(reader, 3);
    var name = new Name(Id: id, Value: string.Empty, Type: type, Parent: GetNamePlaceholder(parentId));

    return name;
  }

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public TableNames(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Names (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Value TEXT NOT NULL,
          Type INTEGER NOT NULL,
          ParentId INTEGER,
          FOREIGN KEY(ParentId) REFERENCES Names(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = "CREATE UNIQUE INDEX NamesValueType ON Names(Value, Type);";
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<IReadOnlyDictionary<int, Name>> GetNamesAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, Type, ParentId
      FROM Names;
      """;

    using var reader = await command.ExecuteReaderAsync(token);
    var result = new Dictionary<int, Name>();
    while (await reader.ReadAsync(token))
    {
      var name = CreateName(reader);
      result.Add(name.Id, name);
    }

    foreach (var name in result.Values)
    {
      if (name.Parent is NamePlaceholder placeholder && result.TryGetValue(placeholder.Id, out var parent))
      {
        result[name.Id] = name with { Parent = parent };
      }
    }

    _Items.SetTarget(result);
    return result;
  }

  public async Task<Name?> GetNameAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }
    if (_Items.TryGetTarget(out var items) && items.TryGetValue(id.Value, out var name))
    {
      return name;
    }
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, Type, ParentId
      FROM Names
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", id.Value);

    using var reader = await command.ExecuteReaderAsync(token);
    if (await reader.ReadAsync(token))
    {
      name = CreateName(reader);
      if (name.Parent is NamePlaceholder placeholder)
      {
        name = name with { Parent = await GetNameAsync(placeholder.Id, token) };
      }

      return name;
    }

    return null;
  }

  public async Task<Name> AddNameAsync(string value, NameType type, Name? parent, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Names (Value, Type, ParentId)
      VALUES (@value, @type, @parentId);
      """;
    command.Parameters.AddWithValue("@value", value);
    command.Parameters.AddWithValue("@type", (int)type);
    command.Parameters.AddWithValue("@parentId", parent != null ? parent.Id : DBNull.Value);
    await command.ExecuteNonQueryAsync(token);
    InvalidateItems();

    return new Name(Id: await Document.GetLastInsertRowIdAsync(token), Value: value, Type: type, Parent: parent);
  }
}
