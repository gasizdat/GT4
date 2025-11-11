using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Reflection.PortableExecutable;

namespace GT4.Core.Project;

public partial class TablePersonData : TableBase
{
  public TablePersonData(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS PersonData (
        PersonId INTEGER NOT NULL,
        DataId INTEGER NOT NULL,
        FOREIGN KEY(PersonId) REFERENCES Persons(Id),
        FOREIGN KEY(DataId) REFERENCES Data(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = "CREATE UNIQUE INDEX PersonDataCategory ON PersonData(PersonId, DataId);";
    await command.ExecuteNonQueryAsync(token);
  }

  private async Task<int[]> GetPersonDataIdsAsync(Person person, DataCategory? category, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = """
        SELECT Data.Id
        FROM Data
        INNER JOIN
          PersonData ON PersonData.DataId=Data.Id AND PersonData.PersonId=@personId
        WHERE Data.Category=@category;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = """
        SELECT DataId
        FROM PersonData
        WHERE PersonId=@personId;
        """;
    }
    command.Parameters.AddWithValue("@personId", person.Id);

    await using var reader = await command.ExecuteReaderAsync(token);
    var ret = new List<int>();
    while (await reader.ReadAsync(token))
    {
      ret.Add(reader.GetInt32(0));
    }

    return ret.ToArray();
  }

  public async Task<Data[]> GetPersonDataSetAsync(Person person, DataCategory? category, CancellationToken token)
  {
    var ids = await GetPersonDataIdsAsync(person, category, token);
    var tasks = ids.Select(id => Document.Data.TryGetDataByIdAsync(id, token));
    var datas = await Task.WhenAll(tasks);

    return datas
      .Where(data => data is not null)
      .Select(data => data!)
      .ToArray();
  }

  public async Task AddPersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    var tasks = new List<Task>();

    foreach (var data in dataSet)
    {
      using var command = Document.CreateCommand();

      command.CommandText = """
        INSERT INTO PersonData (PersonId, DataId)
        VALUES (@personId, @dataId);
        """;
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@dataId", data.Id);
      tasks.Add(command.ExecuteNonQueryAsync(token));
    }

    await Task.WhenAll(tasks);
  }

  public async Task UpdatePersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token)
  {
    var oldDataIds = await GetPersonDataSetAsync(person, null, token);
    var newDataIds = dataSet.Select(data => data.Id).ToHashSet();
    var remainedData = new HashSet<int>();
    var tasks = new List<Task>();

    using var transaction = await Document.BeginTransactionAsync(token);

    foreach (var oldData in oldDataIds)
    {
      var isDataRemained = newDataIds.Contains(oldData.Id);
      if (isDataRemained)
      {
        remainedData.Add(oldData.Id);
        continue;
      }

      using var command = Document.CreateCommand();

      command.CommandText = """
        DELETE FROM PersonNames
        WHERE PersonId=@personId AND NameId=@nameId;
        """;
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@nameId", oldData.Id);
      tasks.Add(command.ExecuteNonQueryAsync(token));
    }

    tasks.Add(AddPersonDataSetAsync(person, dataSet.Where(data => !remainedData.Contains(data.Id)).ToArray(), token));

    await Task.WhenAll(tasks);

    transaction.Commit();
  }

  public async Task UpdatePersonDataAsync(Person person, Data? newData, DataCategory dataCategory, CancellationToken token)
  {
    var resource = await GetPersonDataSetAsync(person, dataCategory, token);
    if (resource.Count() > 1)
    {
      throw new ArgumentException($"The data with category '{dataCategory}' has multiple items");
    }

    var oldData = resource.FirstOrDefault();
    if (newData?.Id == oldData?.Id)
    {
      return;
    }

    using var transaction = await Document.BeginTransactionAsync(token);

    if (oldData is not null)
    {
      await RemovePersonDataAsync(person, oldData, token);
    }

    if (newData is not null)
    {
      await AddPersonDataSetAsync(person, [newData], token);
    }

    transaction.Commit();
  }

  public async Task RemovePersonDataAsync(Person person, Data data, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      DELETE FROM PersonData
      WHERE PersonId=@personId, DataId=@dataId;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);
    command.Parameters.AddWithValue("@dataId", data.Id);
    await command.ExecuteNonQueryAsync(token);

    try
    {
      await Document.Data.RemoveDataAsync(data, token);
    }
    catch { /* The data content still in use */ }

    transaction.Commit();
  }
}
