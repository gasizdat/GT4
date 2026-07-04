using GT4.Core.Gedcom;
using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Converters;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI;

public class GT4Services
{
  public static void Add(IServiceCollection serviceCollection)
  {
    var configurationRoot = new ConfigurationBuilder()
      .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
      .AddAppConfiguration()
      .Build();

    serviceCollection
      .AddSingleton<IConfiguration>(configurationRoot)
      .AddActiveConfigurations(configurationRoot)
      .AddUIUtils()
      .AddCoreUtils()
      .AddDefaultProject()
      .AddGedcom()
      // The GEDCOM residue converter lives here, not in AddUIUtils: it bridges to Core.Gedcom, and the
      // leaf UI.Utils library must stay free of that feature dependency.
      .AddKeyedSingleton<IDataConverter, GedcomDataConverter>(DataCategory.PersonGedcomTags)
      .AddSingleton<IAlertService, RealAlertService>()
      .AddSingleton<INavigationService, RealNavigationService>();
  }

  public static IServiceProvider Provider =>
    IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("DI container not available.");
}
