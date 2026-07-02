using Microsoft.Extensions.DependencyInjection;

namespace GT4.UI.Logic;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddUILogic(this IServiceCollection services) =>
    services.AddSingleton<ProjectListLogic>();
}
