using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public partial class TablePersonData : TableBase
{
  public TablePersonData(ProjectDocument document) : base(document)
  {
  }

  private async Task<PersonData?> TryCreatePersonDataAsync(Person person, SqliteDataReader reader, CancellationToken token)
  {
    var dataId = reader.GetInt32(0);
    var category = GetEnum<DataCategory>(reader, 1);
    var data = await Document.Data.TryGetDataAsync(dataId, token);
    return data is not null ? new PersonData(Person: person, Data: data, Category: category) : null;
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS PersonData (
        PersonId INTEGER NOT NULL,
        DataId INTEGER NOT NULL,
        Category INTEGER NOT NULL,
        FOREIGN KEY(PersonId) REFERENCES Persons(Id),
        FOREIGN KEY(DataId) REFERENCES Data(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = "CREATE UNIQUE INDEX PersonDataCategory ON PersonData(PersonId, DataId, Category);";
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task<PersonData[]> GetPersonDataAsync(Person person, DataCategory? category, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = """
        SELECT Id, DataId, Category
        FROM PersonData
        WHERE PersonId=@personId AND Category=@category;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = """
        SELECT DataId, Category
        FROM PersonData
        WHERE PersonId=@personId;
        """;
    }
    command.Parameters.AddWithValue("@personId", person.Id);

    await using var reader = await command.ExecuteReaderAsync(token);
    var tasks = new List<Task<PersonData?>>();
    while (await reader.ReadAsync(token))
    {
      tasks.Add(TryCreatePersonDataAsync(person, reader, token));
    }

    return (await Task.WhenAll(tasks))
      .Where(i => i is not null)
      .Select(i => i!)
      .ToArray() ?? [];
  }

  public async Task AddPersonDataAsync(Person person, Data data, DataCategory category, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    command.CommandText = """
      INSERT INTO PersonData (PersonId, DataId, Category)
      VALUES (@personId, @dataId, @category);
      """;
    command.Parameters.AddWithValue("@personId", person.Id);
    command.Parameters.AddWithValue("@dataId", data.Id);
    command.Parameters.AddWithValue("@category", category);
    await command.ExecuteNonQueryAsync(token);
  }

  public async Task UpdateDatasAsync(Person person, PersonData[] datas, CancellationToken token)
  {
    throw new NotImplementedException(nameof(UpdateDatasAsync));
  }

  public async Task RemoveDataAsync(Person person, PersonData personData, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      DELETE FROM PersonData
      WHERE PersonId=@personId, DataId=@dataId, Category=@category;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);
    command.Parameters.AddWithValue("@dataId", personData.Data.Id);
    command.Parameters.AddWithValue("@category", personData.Category);
    await command.ExecuteNonQueryAsync(token);

    try
    {
      await Document.Data.RemoveDataAsync(personData.Data, token);
    }
    catch { /* The data content still in use */ }

    transaction.Commit();
  }

  public async Task UpdatePersonSingleDataAsync(Person person, Data? newData, DataCategory dataCategory, CancellationToken token)
  {
    var resource = await GetPersonDataAsync(person, dataCategory, token);
    if (resource.Count() > 1)
    {
      throw new ArgumentException($"The data with category '{dataCategory}' has multiple items");
    }

    var oldData = resource.FirstOrDefault();
    if (newData?.Id == oldData?.Data.Id)
    {
      return;
    }
   
    using var transaction = await Document.BeginTransactionAsync(token);

    if (oldData is not null)
    {
      await RemoveDataAsync(person, oldData, token);
    }

    if (newData is not null)
    {
      await AddPersonDataAsync(person, newData, dataCategory, token);
    }

    transaction.Commit();
  }
}
