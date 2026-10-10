using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Extensions;

public static class PersonManagerExtensions
{
  public static async Task<PersonInfo[]> GetFamilyMembersAsync(
    this IPersonManager personManager,
    Name familyName,
    CancellationToken token)
  {
    if (familyName.Id != NoFamily.Name.Id)
    {
      return await personManager.GetPersonInfosByNameAsync(name: familyName, selectMainPhoto: true, token);
    }

    var allPersons = await personManager.GetPersonInfosAsync(selectMainPhoto: true, token);
    return [.. allPersons.Where(NoFamily.Includes)];
  }
}
