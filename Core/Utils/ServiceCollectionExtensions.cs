using Microsoft.Extensions.DependencyInjection;

namespace GT4.Utils;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection BuildDefaultUtils(this IServiceCollection services)
  {
    return services
      .AddSingleton<IStorage, Storage>()
      .AddSingleton<IFileSystem, FileSystem>();
  }
}
