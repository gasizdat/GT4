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

    var ret = services.GetRequiredService<IConfigurationProvider>();
    builder.Properties[nameof(AppConfigurationProvider)] = ret;

    return ret;
  }
}
