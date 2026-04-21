using Microsoft.Extensions.Configuration;

namespace GT4.Core.Utils;

internal class AppConfigurationSource : IConfigurationSource
{
  public IConfigurationProvider Build(IConfigurationBuilder builder)
  {
    var services = new ServiceCollection()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IConfigurationProvider, AppConfigurationProvider>()
      .BuildServiceProvider(); 

    return services.GetRequiredService<IConfigurationProvider>();
  }
}
