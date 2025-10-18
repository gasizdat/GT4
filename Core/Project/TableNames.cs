namespace GT4.Core.Project;

public class TableNames : TableBase
{
  private record NamePlaceholder(int Id) : Name(Id: Id, Value: string.Empty, Type: NameType.MaleName, null);
  private readonly WeakReference<IReadOnlyDictionary<int, Name>?> _Items = new(null);

  private static Name? GetNamePlaceholder(int? id) =>
    id == null ? null : new NamePlaceholder(id.Value);

  private async Task<IReadOnlyDictionary<int, Name>> GetItemsAsync()
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, Type, ParentId
      FROM Names;
      """;

    using var reader = await command.ExecuteReaderAsync();
    var result = new Dictionary<int, Name>();
    while (await reader.ReadAsync())
    {
      var id = reader.GetInt32(0);
      var value = reader.GetString(1);
      var type = (NameType)reader.GetInt32(2);
      var parentId = TryGetInteger(reader, 3);
      var name = new Name(Id: id, Value: string.Empty, Type: type, Parent: GetNamePlaceholder(parentId));

      result.Add(id, name);
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

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public TableNames(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync()
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
    await command.ExecuteNonQueryAsync();

    command.CommandText = "CREATE UNIQUE INDEX NamesValueType ON Names(Value, Type);";
    await command.ExecuteNonQueryAsync();
  }

  public Task<IReadOnlyDictionary<int, Name>> Names => GetItemsAsync();

  public record class Name(int Id, string Value, NameType Type, Name? Parent);
}
