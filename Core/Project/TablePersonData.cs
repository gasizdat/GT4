using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal partial class TablePersonData : TableOwnerData, ITablePersonData
{
  public TablePersonData(IProjectConnection connection, ITableData data)
    : base(connection, data, tableName: "PersonData", ownerTable: "Persons", ownerColumn: "PersonId", indexName: "PersonDataCategory")
  {
  }

  public Task<Data[]> GetPersonDataSetAsync(Person person, DataCategory? category, CancellationToken token) =>
    GetDataSetAsync(person.Id, category, token);

  public Task<Dictionary<int, Data[]>> GetPersonDataSetAsync(Person[] persons, DataCategory? category, CancellationToken token) =>
    GetDataSetAsync([.. persons.Select(p => p.Id)], category, token);

  public Task AddPersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token) =>
    AddDataSetAsync(person.Id, dataSet, token);

  public Task UpdatePersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token) =>
    UpdateDataSetAsync(person.Id, dataSet, token);

  public Task UpdatePersonDataAsync(Person person, Data? newData, DataCategory dataCategory, CancellationToken token) =>
    UpdateDataAsync(person.Id, newData, dataCategory, token);

  public Task RemovePersonDataAsync(Person person, Data data, CancellationToken token) =>
    RemoveDataAsync(person.Id, data, token);
}
