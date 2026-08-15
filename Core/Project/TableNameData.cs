using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class TableNameData : TableOwnerData<Name>, ITableNameData
{
  public TableNameData(IProjectConnection connection, ITableData data)
    : base(connection, data, tableName: "NameData", ownerTable: "Names", ownerColumn: "NameId", indexName: "NameDataCategory")
  {
  }

  public Task<Data[]> GetNameDataSetAsync(Name name, DataCategory? category, CancellationToken token) =>
    GetDataSetAsync(name, category, token);

  public Task<Dictionary<int, Data[]>> GetNameDataSetAsync(Name[] names, DataCategory? category, CancellationToken token) =>
    GetDataSetAsync(names, category, token);

  public Task<Dictionary<int, int[]>> GetNameIdsByDataAsync(CancellationToken token) =>
    GetDataOwnerIdsAsync(token);

  public Task AddNameDataSetAsync(Name name, Data[] dataSet, CancellationToken token) =>
    AddDataSetAsync(name, dataSet, token);

  public Task UpdateNameDataSetAsync(Name name, Data[] dataSet, CancellationToken token) =>
    UpdateDataSetAsync(name, dataSet, token);

  public Task UpdateNameDataAsync(Name name, Data? newData, DataCategory dataCategory, CancellationToken token) =>
    UpdateDataAsync(name, newData, dataCategory, token);

  public Task RemoveNameDataAsync(Name name, Data data, CancellationToken token) =>
    RemoveDataAsync(name, data, token);
}
