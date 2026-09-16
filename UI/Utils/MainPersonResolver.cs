using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using GT4.UI.Resources;

namespace GT4.UI.Utils;

public sealed class MainPersonResolver
{
  private readonly IMainPersonStore _Store;
  private readonly IProjectList _ProjectList;
  private readonly IAlertService _AlertService;

  public MainPersonResolver(IMainPersonStore store, IProjectList projectList, IAlertService alertService)
  {
    _Store = store;
    _ProjectList = projectList;
    _AlertService = alertService;
  }

  public async Task<PersonInfo?> TryResolveAsync(ProjectInfo project, IProjectDocument document, CancellationToken token)
  {
    var personId = _Store.Get(project);
    if (personId is null)
    {
      return null;
    }

    var person = await document.Persons.TryGetPersonByIdAsync(personId.Value, token);
    if (person is null)
    {
      _Store.Clear(project);
      // The list page reads ProjectInfo.MainPerson straight off ProjectList's cached listing, so
      // clearing a dangling mark here needs the same cache drop PersonPage.OnToggleMainPerson does.
      _ProjectList.InvalidateItems();
      await _AlertService.ShowWarningAsync(UIStrings.AlertTextMainPersonMissing);
      return null;
    }

    var infos = await document.PersonManager.GetPersonInfosAsync([person], selectMainPhoto: true, token);
    return infos.Length > 0 ? infos[0] : null;
  }
}
