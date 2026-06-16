using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITablePersonNames
{
  Task AddPersonNamesAsync(Person person, Name[] names, CancellationToken token);
  Task<Name[]> GetPersonNamesAsync(Person person, CancellationToken token);
  Task<Dictionary<int, Name[]>> GetPersonNamesAsync(int[] personIds, CancellationToken token);
  Task UpdatePersonNamesAsync(Person person, Name[] names, CancellationToken token);
}