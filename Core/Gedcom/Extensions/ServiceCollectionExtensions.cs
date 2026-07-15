using GT4.Core.Gedcom;
using GT4.Core.Gedcom.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.Core.Gedcom.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGedcom(this IServiceCollection services)
  {
    return services
      .AddSingleton<IGedcomImporter, GedcomImporter>()
      .AddSingleton<IGedcomExporter, GedcomExporter>();
  }
}
