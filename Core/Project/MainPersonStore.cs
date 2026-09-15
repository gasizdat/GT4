using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace GT4.Core.Project;

internal sealed class MainPersonStore : IMainPersonStore
{
  private const string KeyPrefix = "MainPerson";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public MainPersonStore(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  public MainPersonInfo? Get(FileDescription origin)
  {
    var (idKey, nameKey) = KeysFor(origin);
    var name = _Configuration[nameKey];
    return int.TryParse(_Configuration[idKey], out var personId) && name is not null
      ? new MainPersonInfo(personId, name)
      : null;
  }

  public void Set(FileDescription origin, int personId, string displayName)
  {
    var (idKey, nameKey) = KeysFor(origin);
    _InteractiveConfiguration?.SetKey(idKey, personId.ToString());
    _InteractiveConfiguration?.SetKey(nameKey, displayName);
  }

  public void Clear(FileDescription origin)
  {
    var (idKey, nameKey) = KeysFor(origin);
    _InteractiveConfiguration?.RemoveKey(idKey);
    _InteractiveConfiguration?.RemoveKey(nameKey);
  }

  // Origin is a path, and ':' (a Windows drive letter) collides with the flat config store's own
  // section delimiter -- hashing sidesteps sanitizing arbitrary path segments entirely.
  private static (string IdKey, string NameKey) KeysFor(FileDescription origin)
  {
    var raw = $"{origin.Directory.Root}|{string.Join('/', origin.Directory.Path)}|{origin.FileName}";
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    var key = $"{KeyPrefix}.{hash}";
    return (key, $"{key}.Name");
  }
}
