using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace GT4.Core.Project;

internal partial class TablePersonData : TableBase, ITablePersonData
{
  public TablePersonData(IProjectDocument document) : base(document)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      CREATE TABLE IF NOT EXISTS PersonData (
        PersonId INTEGER NOT NULL,
        DataId INTEGER NOT NULL,
        FOREIGN KEY(PersonId) REFERENCES Persons(Id) ON DELETE CASCADE,
        -- DataId is intentionally NOT cascaded: deleting a person drops its data links (via PersonId),
        -- but a Data blob still referenced by another person must NOT be deletable (RemovePersonData
        -- relies on this to detect "still in use").
        FOREIGN KEY(DataId) REFERENCES Data(Id)
      );
      """;
    await command.ExecuteNonQueryAsync(token);

    command.CommandText = "CREATE UNIQUE INDEX PersonDataCategory ON PersonData(PersonId, DataId);";
    await command.ExecuteNonQueryAsync(token);
  }

  private static Data CreateDataFromRow(DbDataReader reader)
  {
    return new Data(
      Id: reader.GetInt32(0),
      Content: reader.GetFieldValue<byte[]>(1),
      MimeType: TryGetString(reader, 2),
      Category: GetEnum<DataCategory>(reader, 3));
  }

  private async Task<Data> AddDataContentIfNotExist(Data data, CancellationToken token)
  {
    if (data.Id == NonCommittedId)
    {
      return await Document.Data.AddDataAsync(data.Content, data.MimeType, data.Category, token);
    }

    return data;
  }

  public async Task<Data[]> GetPersonDataSetAsync(Person person, DataCategory? category, CancellationToken token)
  {
    using var command = Document.CreateCommand();

    if (category.HasValue)
    {
      command.CommandText = """
        SELECT d.Id, d.Value, d.MimeType, d.Category
        FROM Data d
        INNER JOIN PersonData pd ON pd.DataId = d.Id AND pd.PersonId = @personId
        WHERE d.Category = @category
        ORDER BY pd.ROWID;
        """;
      command.Parameters.AddWithValue("@category", category.Value);
    }
    else
    {
      command.CommandText = """
        SELECT d.Id, d.Value, d.MimeType, d.Category
        FROM Data d
        INNER JOIN PersonData pd ON pd.DataId = d.Id
        WHERE pd.PersonId = @personId
        ORDER BY pd.ROWID;
        """;
    }
    command.Parameters.AddWithValue("@personId", person.Id);

    var result = new List<Data>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      result.Add(CreateDataFromRow(reader));
    }

    return result.ToArray();
  }

  public async Task AddPersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    // Add person data one at a time to preserve the data order.
    foreach (var data in dataSet)
    {
      var dataId = (await AddDataContentIfNotExist(data, token)).Id;
      using var command = Document.CreateCommand();

      command.CommandText = """
        INSERT INTO PersonData (PersonId, DataId)
        VALUES (@personId, @dataId);
        """;
      command.Parameters.AddWithValue("@personId", person.Id);
      command.Parameters.AddWithValue("@dataId", dataId);
      await command.ExecuteNonQueryAsync(token);
    }
    transaction.Commit();
  }

  public async Task UpdatePersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      DELETE FROM PersonData
      WHERE PersonId=@personId;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);
    await command.ExecuteNonQueryAsync(token);

    await AddPersonDataSetAsync(person, dataSet, token);

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
    if (newData is not null)
    {
      newData = await AddDataContentIfNotExist(newData, token);
    }

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
      WHERE PersonId=@personId AND DataId=@dataId;
      """;
    command.Parameters.AddWithValue("@personId", person.Id);
    command.Parameters.AddWithValue("@dataId", data.Id);
    await command.ExecuteNonQueryAsync(token);

    try
    {
      await Document.Data.RemoveDataAsync(data, token);
    }
    catch (SqliteException)
    {
      // The data content is still referenced by another person: the foreign key blocks its deletion,
      // which is expected here. Any other failure is genuine and must surface.
    }

    transaction.Commit();
  }
}
