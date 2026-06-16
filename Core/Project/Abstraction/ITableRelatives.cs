using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITableRelatives
{
  Task AddRelativesAsync(Person person, Relative[] relatives, CancellationToken token);
  Task<Relative[]> GetRelativesAsync(Person person, CancellationToken token);
  Task<Dictionary<int, Relative[]>> GetRelativesForPersonsAsync(int[] personIds, CancellationToken token);
  Task<bool> HasCommonAncestorsAsync(Person personA, Person personB, CancellationToken token);
  Task UpdateRelativesAsync(Person person, Relative[] relatives, CancellationToken token);
}