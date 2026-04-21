using Microsoft.Extensions.Configuration;

namespace GT4.Core.Utils;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddDefaultUtils(this IServiceCollection services)
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

  public static IServiceCollection AddActiveConfigurations(this IServiceCollection services, IConfigurationBuilder configurationBuilder)
  {
    foreach (var (key, value) in configurationBuilder.Properties)
    {
      if (value is IActiveConfiguration activeConfiguration)
      {
        services = services.AddKeyedSingleton(key, activeConfiguration);
      }
    }

    return services;
  }
}
