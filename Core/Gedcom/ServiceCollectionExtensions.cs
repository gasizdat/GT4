using GT4.Core.Gedcom.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Gedcom;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGedcom(this IServiceCollection services)
  {
    return services
      .AddSingleton<IGedcomImporter, GedcomImporter>()
      .AddSingleton<IGedcomExporter, GedcomExporter>();
  }
}
