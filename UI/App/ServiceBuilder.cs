using Microsoft.Extensions.DependencyInjection;

namespace GT4.Utils;

public class ServiceBuilder
{
  public static ServiceProvider DefaultServices => new ServiceCollection()
        .AddSingleton<IStorage, Storage>()
        .AddSingleton<IFileSystem, FileSystem>()
        .AddSingleton<IProjectList, ProjectList>()
        .BuildServiceProvider();
}
