using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Utils;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection BuildDefaultUtils(this IServiceCollection services)
  {
    return services
      .AddSingleton<IStorage, Storage>()
      .AddSingleton<IFileSystem, FileSystem>();
  }
}
