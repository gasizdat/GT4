using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITableNameData
{
  Task AddNameDataSetAsync(Name name, Data[] dataSet, CancellationToken token);
  Task<Data[]> GetNameDataSetAsync(Name name, DataCategory? category, CancellationToken token);
  Task<Dictionary<int, Data[]>> GetNameDataSetAsync(Name[] names, DataCategory? category, CancellationToken token);
  Task RemoveNameDataAsync(Name name, Data data, CancellationToken token);
  Task UpdateNameDataAsync(Name name, Data? newData, DataCategory dataCategory, CancellationToken token);
  Task UpdateNameDataSetAsync(Name name, Data[] dataSet, CancellationToken token);
}
