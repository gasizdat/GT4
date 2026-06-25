using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using System.Data.Common;

namespace GT4.Core.Project;

internal class TableNames : TableBase, ITableNames
{
  private static Name CreateName(DbDataReader reader)
  {
    var id = reader.GetInt32(0);
    var value = reader.GetString(1);
    var type = GetEnum<NameType>(reader, 2);
    var parentId = TryGetInteger(reader, 3);
    var name = new Name(Id: id, Value: value, Type: type, ParentId: parentId);

    return name;
  }

  public TableNames(IProjectDocument document) : base(document)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Names (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Value TEXT NOT NULL,
          Type INTEGER NOT NULL,
          ParentId INTEGER,
          FOREIGN KEY(ParentId) REFERENCES Names(Id) ON DELETE CASCADE
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = "CREATE UNIQUE INDEX NamesValueType ON Names(Value, Type, ParentId);";
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Name[]> GetNamesByTypeAsync(NameType nameType, CancellationToken token)
  {
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

    return result
      .Values
      .ToArray();
  }

  public async Task<Name?> TryGetNameByIdAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Value, Type, ParentId
      FROM Names
      WHERE Id=@id;
      """;
    command.Parameters.AddWithValue("@id", id.Value);

    await using var reader = await command.ExecuteReaderAsync(token);
    return (await reader.ReadAsync(token)) ? CreateName(reader) : null;
  }

  public async Task<Name[]?> TryGetNameWithSubnamesByIdAsync(int? id, CancellationToken token)
  {
    if (!id.HasValue)
    {
      return null;
    }

    using var command = Document.CreateCommand();
    command.CommandText = """
      SELECT t1.Id, t1.Value, t1.Type, t1.ParentId
      FROM Names AS t1
      INNER JOIN Names AS t2 ON t2.Id=@id AND (t2.ParentId=t1.Id OR t2.ParentId=t1.ParentId OR t1.Id=@id OR t1.ParentId=@id);
      """;
    command.Parameters.AddWithValue("@id", id.Value);

    await using var reader = await command.ExecuteReaderAsync(token);
    var ret = new List<Name>();
    while (await reader.ReadAsync(token))
    {
      ret.Add(CreateName(reader));
    }

    return ret.Count > 0 ? ret.ToArray() : null;
  }

  public async Task<Name> AddNameAsync(string value, NameType type, Name? parent, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT INTO Names (Value, Type, ParentId)
      VALUES (@value, @type, @parentId)
      RETURNING Id;
      """;
    command.Parameters.AddWithValue("@value", value);
    command.Parameters.AddWithValue("@type", (int)type);
    command.Parameters.AddWithValue("@parentId", parent != null ? parent.Id : DBNull.Value);

    var insertedId = await command.ExecuteScalarAsync(token);
    var ret = new Name(Id: Convert.ToInt32(insertedId), Value: value, Type: type, ParentId: parent?.Id);
    await transaction.CommitAsync(token);

    return ret;
  }

  public async Task<Name> AddFirstMaleNameAsync(string firstName, string? malePatronymic, string? femalePatronymic, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    var name = await AddNameAsync(firstName, NameType.FirstName | NameType.MaleDeclension, null, token);

    // Sequential: writes inside a transaction must run one at a time on the single connection.
    if (malePatronymic is not null)
    {
      await AddNameAsync(malePatronymic, NameType.Patronymic | NameType.MaleDeclension, name, token);
    }
    if (femalePatronymic is not null)
    {
      await AddNameAsync(femalePatronymic, NameType.Patronymic | NameType.FemaleDeclension, name, token);
    }
    await transaction.CommitAsync(token);

    return name;
  }

  public async Task<Name> AddFirstFemaleNameAsync(string firstName, CancellationToken token)
  {
    var name = await AddNameAsync(firstName, NameType.FirstName | NameType.FemaleDeclension, null, token);
    return name;
  }

  public async Task UpdateName(Name name, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      UPDATE Names
      SET Value=@value
      WHERE Id=@nameId;
      """;
    command.Parameters.AddWithValue("@value", name.Value);
    command.Parameters.AddWithValue("@nameId", name.Id);
    await command.ExecuteNonQueryAsync(token);
    await transaction.CommitAsync(token);
  }

  public async Task RemoveNameWithSubnamesAsync(Name name, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      DELETE FROM Names
      WHERE Id=@id OR ParentId=@id;
      """;
    command.Parameters.AddWithValue("@id", name.Id);
    await command.ExecuteNonQueryAsync(token);
    await transaction.CommitAsync(token);
  }
}
