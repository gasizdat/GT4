using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITableRelatives
{
  Task AddRelativesAsync(Person person, Relative[] relatives, CancellationToken token);
  Task<Relative[]> GetRelativesAsync(Person person, CancellationToken token);
  Task UpdateRelativesAsync(Person person, Relative[] relatives, CancellationToken token);
}