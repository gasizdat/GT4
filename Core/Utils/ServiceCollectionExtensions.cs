using Microsoft.Extensions.Configuration;

namespace GT4.Core.Utils;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddCoreUtils(this IServiceCollection services)
  {
    return services
#if ANDROID
      .AddSingleton<IFileSystem, AndroidFileSystem>()
#elif WINDOWS
      .AddSingleton<IFileSystem, FileSystem>()
#endif
      .AddSingleton<IStorage, Storage>()
      .AddSingleton<ICancellationTokenProvider, CancellationTokenProvider>();
  }

  public static IServiceCollection AddActiveConfigurations(this IServiceCollection services, IConfigurationRoot configurationRoot)
  {
    foreach (var provider in configurationRoot.Providers)
    {
      if (provider is IInteractiveConfiguration interactiveConfiguration)
      {
        services = services.AddKeyedSingleton(interactiveConfiguration.Name, interactiveConfiguration);
      }
    }

    return services;
  }
}
