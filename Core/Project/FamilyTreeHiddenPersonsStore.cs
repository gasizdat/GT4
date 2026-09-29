using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GT4.Core.Project;

internal sealed partial class FamilyTreeHiddenPersonsStore : IFamilyTreeHiddenPersonsStore
{
  [JsonSerializable(typeof(int[]))]
  private partial class JsonContext : JsonSerializerContext
  {
  }

  private const string IdsKey = "FamilyTree.HiddenPersons.Ids";

  private readonly ProjectConfigurationProvider.Factory _Factory;

  public FamilyTreeHiddenPersonsStore(ProjectConfigurationProvider.Factory factory)
  {
    _Factory = factory;
  }

  public int[] Get(ProjectInfo project)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();

    return provider.TryGet(IdsKey, out var json) && json is not null
      ? JsonSerializer.Deserialize(json, JsonContext.Default.Int32Array) ?? []
      : [];
  }

  public void Set(ProjectInfo project, IEnumerable<int> personIds)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    int[] ids = [.. personIds];
    var json = JsonSerializer.Serialize(ids, JsonContext.Default.Int32Array);
    provider.SetKey(IdsKey, json);
    provider.Flush();
  }
}
