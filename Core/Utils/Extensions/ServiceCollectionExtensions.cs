using GT4.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Utils.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddCoreUtils(this IServiceCollection services)
  {
    return services
#if ANDROID
      // Android scoped storage needs MediaStore; every other target uses direct file access.
      .AddSingleton<IFileSystem, AndroidFileSystem>()
#else
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
