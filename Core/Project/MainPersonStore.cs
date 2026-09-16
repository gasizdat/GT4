using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.Core.Project;

internal sealed class MainPersonStore : IMainPersonStore
{
  private const string IdKey = "MainPerson.Id";

  private readonly ProjectConfigurationProvider.Factory _Factory;

  public MainPersonStore(ProjectConfigurationProvider.Factory factory)
  {
    _Factory = factory;
  }

  public int? Get(ProjectInfo project)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();

    return provider.TryGet(IdKey, out var idValue) && int.TryParse(idValue, out var personId) ? personId : null;
  }

  public void Set(ProjectInfo project, int personId)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    provider.SetKey(IdKey, personId.ToString());
    provider.Flush();
  }

  public void Clear(ProjectInfo project)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    provider.RemoveKey(IdKey);
    provider.Flush();
  }
}
