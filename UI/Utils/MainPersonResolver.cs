using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.Abstraction;
using GT4.UI.Resources;

namespace GT4.UI.Utils;

public sealed class MainPersonResolver
{
  private readonly IMainPersonStore _Store;
  private readonly IAlertService _AlertService;

  public MainPersonResolver(IMainPersonStore store, IAlertService alertService)
  {
    _Store = store;
    _AlertService = alertService;
  }

  public async Task<PersonInfo?> TryResolveAsync(ProjectInfo project, IProjectDocument document, CancellationToken token)
  {
    var stored = _Store.Get(project);
    if (stored is null)
    {
      return null;
    }

    var person = await document.Persons.TryGetPersonByIdAsync(stored.PersonId, token);
    if (person is null)
    {
      _Store.Clear(project);
      await _AlertService.ShowWarningAsync(UIStrings.AlertTextMainPersonMissing);
      return null;
    }

    var infos = await document.PersonManager.GetPersonInfosAsync([person], selectMainPhoto: true, token);
    var info = infos.Length > 0 ? infos[0] : null;

    // Keeps the ProjectListPage card badge from drifting after a rename, GEDCOM re-import, or
    // revision restore -- covers every resolution call site for free instead of hooking each one.
    if (info is not null && info.DisplayName != stored.DisplayName)
    {
      _Store.Set(project, info.Id, info.DisplayName);
    }

    return info;
  }
}
