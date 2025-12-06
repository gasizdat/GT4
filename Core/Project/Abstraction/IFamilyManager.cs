using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IFamilyManager
{
  Task<Name> AddFamilyAsync(string familyName, string maleLastName, string femaleLastName, CancellationToken token);
  Task<Name[]> GetFamiliesAsync(CancellationToken token);
  Task<Name[]> GetRequiredNames(Name familyName, PersonInfo personInfo, CancellationToken token);
  Task RemoveFamilyAsync(Name familyName, CancellationToken token);
  TPerson SetUpPersonFamily<TPerson>(TPerson person, Name familyName) where TPerson : PersonInfo;
  Task UpdateFamilyAsync(Name familyName, Name? maleLastName, Name? femaleLastName, CancellationToken token);
}