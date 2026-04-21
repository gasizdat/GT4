using Microsoft.Extensions.Configuration;

namespace GT4.Core.Utils;

internal class AppConfigurationProvider : ConfigurationProvider, IActiveConfiguration
{
  private readonly IFileSystem _FileSystem;

  public AppConfigurationProvider(IFileSystem fileSystem)
  {
    _FileSystem = fileSystem;
  }

  public void SetKey(string key, string value)
  {
    lock (Data)
    {
      Data[key]  = value;
      OnReload();
    }
  }

  public void RemoveKey(string key)
  {
    lock (Data)
    {
      Data.Remove(key);
      OnReload();
    }
  }
}
