using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Reflection.Metadata;

namespace GT4.Core.Project;

public partial class TablePersonNames : TableBase
{
  public TablePersonNames(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS PersonNames (
        FOREIGN KEY(PersonId) REFERENCES Persons(Id),
        FOREIGN KEY(NameId) REFERENCES Names(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<Name[]> GetPersonNamesAsync(int personId, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      SELECT NameId
      FROM PersonNames
      WHERE PersonId=@personId;
      """;
    command.Parameters.AddWithValue("@personId", personId);

    using var reader = await command.ExecuteReaderAsync(token);
    var result = new List<Name>();
    while (await reader.ReadAsync(token))
    {
      var id = reader.GetInt32(0);
      var name = await Document.Names.GetNameAsync(id, token);
      if (name is not null)
      {
        result.Add(name);
      }
    }

    return result.ToArray();
  }

  public async Task AddNamesAsync(int personId, Name[] names, CancellationToken token)
  {
    var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
      command.CommandText = """
      INSERT INTO PersonNames (PersonId, NameId)
      VALUES (@personId, @nameId);
      """;

    foreach (var name in names)
    {
      command.Parameters.AddWithValue("@personId", personId);
      command.Parameters.AddWithValue("@nameId", name.Id);
      await command.ExecuteNonQueryAsync(token);
    }
  }
}
