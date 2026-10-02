using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GT4.Core.Project;

internal sealed partial class FamilyTreeArrangementStore : IFamilyTreeArrangementStore
{
  [JsonSerializable(typeof(Dictionary<int, Dictionary<int, double>>))]
  private partial class JsonContext : JsonSerializerContext
  {
  }

  // Named for the box layout, so another tree layout never inherits its columns.
  private const string ArrangementsKey = "FamilyTree.Box.Arrangements";

  private readonly ProjectConfigurationProvider.Factory _Factory;

  public FamilyTreeArrangementStore(ProjectConfigurationProvider.Factory factory)
  {
    _Factory = factory;
  }

  public IReadOnlyDictionary<int, double> Get(ProjectInfo project, int centerId)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();

    return Read(provider).GetValueOrDefault(centerId) ?? [];
  }

  public void Set(ProjectInfo project, int centerId, IReadOnlyDictionary<int, double> offsets)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    var arrangements = Read(provider);
    arrangements[centerId] = new Dictionary<int, double>(offsets);
    Write(provider, arrangements);
  }

  public void Clear(ProjectInfo project, int centerId)
  {
    var provider = _Factory.Create(project.Origin);
    provider.Load();
    var arrangements = Read(provider);
    arrangements.Remove(centerId);
    Write(provider, arrangements);
  }

  private static Dictionary<int, Dictionary<int, double>> Read(ProjectConfigurationProvider provider) =>
    provider.TryGet(ArrangementsKey, out var json) && json is not null
      ? JsonSerializer.Deserialize(json, JsonContext.Default.DictionaryInt32DictionaryInt32Double) ?? []
      : [];

  private static void Write(ProjectConfigurationProvider provider, Dictionary<int, Dictionary<int, double>> arrangements)
  {
    var json = JsonSerializer.Serialize(arrangements, JsonContext.Default.DictionaryInt32DictionaryInt32Double);
    provider.SetKey(ArrangementsKey, json);
    provider.Flush();
  }
}
