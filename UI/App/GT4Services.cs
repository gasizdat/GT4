using GT4.Core.Gedcom.Extensions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.Core.Utils.Extensions;
using GT4.UI.Abstraction;
using GT4.UI.Converters;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
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
      // These converters live here, not in AddUIUtils: they bridge to Core.Gedcom, and the leaf
      // UI.Utils library must stay free of that feature dependency.
      .AddKeyedSingleton<IDataConverter, GedcomDataConverter>(DataCategory.PersonGedcomTags)
      .AddKeyedSingleton<IDataConverter, PhotoTagDataConverter>(DataCategory.PersonMainPhotoTagged)
      .AddKeyedSingleton<IDataConverter, PhotoTagDataConverter>(DataCategory.PersonPhotoTagged)
      .AddSingleton<IAlertService, AlertService>()
      .AddSingleton<INavigationService, NavigationService>()
      .AddSingleton<IProjectRevisionMonitor, ProjectRevisionMonitor>()
      .AddSingleton<GedcomImportEncoding>()
      .AddTransient<DataConverterResolver>(sp => category => sp.GetRequiredKeyedService<IDataConverter>(category))
      .AddTransient<OptionalDataConverterResolver>(sp => category => sp.GetKeyedService<IDataConverter>(category))
      .AddTransient<SelectNameDialog.Factory>()
      .AddTransient<SelectRelativesDialog.Factory>()
      .AddTransient<SelectPersonDialog.Factory>()
      .AddTransient<CreateOrUpdatePersonDialog.Factory>();
  }

  public static IServiceProvider Provider =>
    IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("DI container not available.");
}
