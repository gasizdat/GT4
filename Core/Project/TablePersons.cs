using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Reflection.Metadata;

namespace GT4.Core.Project;

public partial class TablePersons : TableBase
{
  private readonly WeakReference<IReadOnlyDictionary<int, Person>?> _Items = new(null);

  public TablePersons(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
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
    await command.ExecuteNonQueryAsync(token);
  }

  private async Task<Person> CreatePersonAsync(SqliteDataReader reader, CancellationToken token)
  {
    var names = await Task.WhenAll(
      Document.Names.GetNameAsync(reader.GetInt32(1), token),
      Document.Names.GetNameAsync(reader.GetInt32(2), token),
      Document.Names.GetNameAsync(reader.GetInt32(3), token),
      Document.Names.GetNameAsync(reader.GetInt32(4), token),
      Document.Names.GetNameAsync(reader.GetInt32(5), token),
      Document.Names.GetNameAsync(reader.GetInt32(6), token),
      Document.Names.GetNameAsync(reader.GetInt32(7), token),
      Document.Names.GetNameAsync(reader.GetInt32(8), token),
      Document.Names.GetNameAsync(reader.GetInt32(9), token),
      Document.Names.GetNameAsync(reader.GetInt32(10), token)
    );

    return new Person
    (
      Id: reader.GetInt32(0),
      Name: names[0]!,
      Name2: names[1],
      Name3: names[2],
      Name4: names[3],
      Name5: names[4],
      Name6: names[5],
      Name7: names[6],
      Name8: names[7],
      Name9: names[8],
      Name10: names[9],
      BirthDate: TryGetDateTime(reader, 11),
      BirthDateStatus: GetEnum<DateStatus>(reader, 12),
      DeathDate: TryGetDateTime(reader, 13),
      DeathDateStatus: TryGetEnum<DateStatus>(reader, 14),
      BiologicalSex: GetEnum<BiologicalSex>(reader, 15)
    );
  }

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public async Task<IReadOnlyDictionary<int, Person>> GetPersonsAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    using var transaction = Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT Id, Name1, Name2, Name3, Name4, Name5, Name6, Name7, Name8, Name9, Name10, 
             BirthDate, BirthDateStatus, DeathDate, DeathDateStatus, BiologicalSex
      FROM Persons;
      """;

    using var reader = await command.ExecuteReaderAsync(token);
    var result = new Dictionary<int, Person>();
    while (await reader.ReadAsync(token))
    {
      var person = await CreatePersonAsync(reader, token);
      result.Add(person.Id, person);
    }

    _Items.SetTarget(result);
    return result;
  }
}
