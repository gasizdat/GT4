using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IPersonManager
{
  Task<PersonInfo> AddPersonAsync(PersonFullInfo personFullInfo, CancellationToken token);
  Task<PersonFullInfo> GetPersonFullInfoAsync(Person person, MainPhoto mainPhoto, CancellationToken token);
  Task<PersonInfo[]> GetPersonInfosAsync(MainPhoto mainPhoto, CancellationToken token);
  Task<PersonInfo[]> GetPersonInfosAsync(Person[] persons, MainPhoto mainPhoto, CancellationToken token);
  Task<PersonInfo[]> GetPersonInfosByNameAsync(Name name, MainPhoto mainPhoto, CancellationToken token);
  Task UpdatePersonAsync(PersonFullInfo personFullInfo, CancellationToken token);
}