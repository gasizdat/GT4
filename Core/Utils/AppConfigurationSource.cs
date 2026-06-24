using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Utils;

internal class AppConfigurationSource : IConfigurationSource
{
  public IConfigurationProvider Build(IConfigurationBuilder builder)
  {
    var services = new ServiceCollection()
      .AddSingleton<IConfigurationProvider, AppConfigurationProvider>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IStorage, Storage>()
      .BuildServiceProvider(); 

    return services.GetRequiredService<IConfigurationProvider>();
  }
}
