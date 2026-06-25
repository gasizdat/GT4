using GT4.Core.Project.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Project;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddDefaultProject(this IServiceCollection services)
  {
    return services
      .AddSingleton<IProjectList, ProjectList>()
      .AddSingleton<ICurrentProjectProvider, CurrentProjectProvider>();
  }
}
