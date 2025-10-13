using Microsoft.Extensions.DependencyInjection;

namespace GT4.Project;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection BuildDefaultProject(this IServiceCollection services)
  {
    return services
      .AddSingleton<IProjectList, ProjectList>();
  }
}
