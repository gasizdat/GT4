using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Utils;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection BuildDefaultUtils(this IServiceCollection services)
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
}
