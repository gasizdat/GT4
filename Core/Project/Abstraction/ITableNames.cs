using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITableNames
{
  Task<Name> AddFirstFemaleNameAsync(string firstName, CancellationToken token);
  Task<Name> AddFirstMaleNameAsync(string firstName, string? maleMiddleName, string? femaleMiddleName, CancellationToken token);
  Task<Name> AddNameAsync(string value, NameType type, Name? parent, CancellationToken token);
  Task<Name[]> GetNamesByTypeAsync(NameType nameType, CancellationToken token);
  Task RemoveNameWithSubnamesAsync(Name name, CancellationToken token);
  Task<Name?> TryGetNameByIdAsync(int? id, CancellationToken token);
  Task<Name[]?> TryGetNameWithSubnamesByIdAsync(int? id, CancellationToken token);
  Task UpdateName(Name name, CancellationToken token);
}