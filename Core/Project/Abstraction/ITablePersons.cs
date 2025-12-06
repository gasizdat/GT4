using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITablePersons
{
  Task<Person> AddPersonAsync(Person person, CancellationToken token);
  Task<Person[]> GetPersonsAsync(CancellationToken token);
  Task<Person?> TryGetPersonByIdAsync(int personId, CancellationToken token);
  Task UpdatePersonAsync(Person person, CancellationToken token);
}