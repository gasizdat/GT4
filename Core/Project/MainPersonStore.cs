using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.Core.Project;

internal sealed class MainPersonStore : IMainPersonStore
{
  private const string IdKey = "MainPerson.Id";
  private const string NameKey = "MainPerson.Name";

  private readonly ProjectConfigurationProvider.Factory _Factory;

  public MainPersonStore(ProjectConfigurationProvider.Factory factory)
  {
    _Factory = factory;
  }

  public MainPersonInfo? Get(ProjectInfo project)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();

    if (!provider.TryGet(IdKey, out var idValue) || !int.TryParse(idValue, out var personId) ||
      !provider.TryGet(NameKey, out var name))
    {
      return null;
    }

    return new MainPersonInfo(personId, name!);
  }

  public void Set(ProjectInfo project, int personId, string displayName)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    provider.SetKey(IdKey, personId.ToString());
    provider.SetKey(NameKey, displayName);
    provider.Flush();
  }

  public void Clear(ProjectInfo project)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    provider.RemoveKey(IdKey);
    provider.RemoveKey(NameKey);
    provider.Flush();
  }
}
