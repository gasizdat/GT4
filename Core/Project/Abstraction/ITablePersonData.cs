using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITablePersonData
{
  Task AddPersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token);
  Task<Data[]> GetPersonDataSetAsync(Person person, DataCategory? category, CancellationToken token);
  Task RemovePersonDataAsync(Person person, Data data, CancellationToken token);
  Task UpdatePersonDataAsync(Person person, Data? newData, DataCategory dataCategory, CancellationToken token);
  Task UpdatePersonDataSetAsync(Person person, Data[] dataSet, CancellationToken token);
}