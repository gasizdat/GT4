using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IPersonManager
{
  Task<PersonInfo> AddPersonAsync(PersonFullInfo personFullInfo, CancellationToken token);
  Task<PersonFullInfo> GetPersonFullInfoAsync(Person person, CancellationToken token);
  Task<PersonInfo[]> GetPersonInfosAsync(bool selectMainPhoto, CancellationToken token);
  Task<PersonInfo[]> GetPersonInfosAsync(Person[] persons, bool selectMainPhoto, CancellationToken token);
  Task<PersonInfo[]> GetPersonInfosByNameAsync(Name name, bool selectMainPhoto, CancellationToken token);
  Task UpdatePersonAsync(PersonFullInfo personFullInfo, CancellationToken token);
}