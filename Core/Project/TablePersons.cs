namespace GT4.Core.Project;

public class TablePersons : TableBase
{
  public TablePersons(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync()
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS Persons (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name1 INTEGER NOT NULL,
        Name2 INTEGER,
        Name3 INTEGER,
        Name4 INTEGER,
        Name5 INTEGER,
        Name6 INTEGER,
        Name7 INTEGER,
        Name8 INTEGER,
        Name9 INTEGER,
        Name10 INTEGER,
        BirthDate NUMERIC,
        BirthDateStatus INTEGER NOT NULL,
        DeathDate NUMERIC,
        DeathDateStatus INTEGER,
        BiologicalSex INTEGER NOT NULL,
        FOREIGN KEY(Name1) REFERENCES Names(Id),
        FOREIGN KEY(Name2) REFERENCES Names(Id),
        FOREIGN KEY(Name3) REFERENCES Names(Id),
        FOREIGN KEY(Name4) REFERENCES Names(Id),
        FOREIGN KEY(Name5) REFERENCES Names(Id),
        FOREIGN KEY(Name6) REFERENCES Names(Id),
        FOREIGN KEY(Name7) REFERENCES Names(Id),
        FOREIGN KEY(Name8) REFERENCES Names(Id),
        FOREIGN KEY(Name9) REFERENCES Names(Id),
        FOREIGN KEY(Name10) REFERENCES Names(Id)
      );
      """;
    await command.ExecuteNonQueryAsync();
  }
}
