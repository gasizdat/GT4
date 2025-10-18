namespace GT4.Core.Project;

public class TableNames : TableBase
{
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
}
