using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Diagnostics.CodeAnalysis;

namespace GT4.Core.Project;

using NameList = WeakReference<IReadOnlyDictionary<int, Name>?>;

public class TableNames : TableBase
{
  private readonly Dictionary<NameType, NameList> _Items = new();

  private static Name CreateName(SqliteDataReader reader)
  {
    var id = reader.GetInt32(0);
    var value = reader.GetString(1);
    var type = GetEnum<NameType>(reader, 2);
    var parentId = TryGetInteger(reader, 3);
    var name = new Name(Id: id, Value: value, Type: type, ParentId: parentId);

    return name;
  }

  private void InvalidateItems(NameType nameType)
  {
    _Items.Remove(nameType);
  }

  private bool TryGetNameList(NameType nameType, [MaybeNullWhen(false)] out IReadOnlyDictionary<int, Name> list)
  {
    list = null;
    return _Items.TryGetValue(nameType, out var items) && items.TryGetTarget(out list);
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

  public async Task<IReadOnlyDictionary<int, Name>> GetNamesAsync(NameType nameType, CancellationToken token)
  {
    if (TryGetNameList(nameType, out var items))
      return items;

    using var command = Document.CreateCommand();

    if (nameType == NameType.AllNames)
    {
      command.CommandText = """
        SELECT Id, Value, Type, ParentId
        FROM Names;
        """;
    }
    else
    {
      command.CommandText = """
        SELECT Id, Value, Type, ParentId
        FROM Names
        WHERE Type=@type;
        """;
      command.Parameters.AddWithValue("@type", nameType);
    }

    await using var reader = await command.ExecuteReaderAsync(token);
    var result = new Dictionary<int, Name>();
    while (await reader.ReadAsync(token))
    {
      var name = CreateName(reader);
      result.Add(name.Id, name);
    }

    _Items.Add(nameType, new NameList(result));
    return result;
  }

  public async Task<Name?> GetNameAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }
    if (TryGetNameList(NameType.AllNames, out var items) && items.TryGetValue(id.Value, out var name))
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

    await using var reader = await command.ExecuteReaderAsync(token);
    if (await reader.ReadAsync(token))
    {
      name = CreateName(reader);
      return name;
    }

    return null;
  }

  public async Task<Name[]?> GetNameWithSubnamesAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }

    List<Name> ret;
    if (TryGetNameList(NameType.AllNames, out var items) && items.TryGetValue(id.Value, out var name))
    {
      ret = items.Values.Where(name => name.ParentId == id.Value).ToList();
      ret.Insert(0, name);
      return ret.ToArray();
    }

    using var command = Document.CreateCommand();
    command.CommandText = """
      SELECT Id, Value, Type, ParentId
      FROM Names
      WHERE Id=@id OR ParentId=@id;
      """;
    command.Parameters.AddWithValue("@id", id.Value);

    await using var reader = await command.ExecuteReaderAsync(token);
    ret = new();
    while (await reader.ReadAsync(token))
    {
      ret.Add(CreateName(reader));
    }

    return ret.Count > 0 ? ret.ToArray() : null;
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
    InvalidateItems(type);

    return new Name(Id: await Document.GetLastInsertRowIdAsync(token), Value: value, Type: type, ParentId: parent?.Id);
  }
}
