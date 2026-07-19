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
      .AddSingleton<GedcomImportEncoding>()
      // Modal dialogs are one-shot and some callers open the same dialog type repeatedly over their
      // own lifetime, so each is resolved fresh per open through a factory delegate rather than
      // injected as a singleton/transient instance -- see issue #122.
      .AddTransient<DataConverterResolver>(sp =>
        category => sp.GetRequiredKeyedService<IDataConverter>(category))
      .AddTransient<SelectNameDialogFactory>(sp =>
        (biologicalSex, nameTypes) => new SelectNameDialog(
          biologicalSex,
          nameTypes,
          sp.GetRequiredService<INameTypeFormatter>(),
          sp.GetRequiredService<ICurrentProjectProvider>(),
          sp.GetRequiredService<ICancellationTokenProvider>(),
          sp.GetRequiredService<IComparer<Name>>(),
          sp.GetRequiredService<IAlertService>()))
      .AddTransient<SelectRelativesDialogFactory>(sp =>
        (biologicalSex, existingRelatives) => new SelectRelativesDialog(
          biologicalSex,
          existingRelatives,
          sp.GetRequiredService<ICancellationTokenProvider>(),
          sp.GetRequiredService<ICurrentProjectProvider>(),
          sp.GetRequiredService<IDateFormatter>(),
          sp.GetRequiredService<IComparer<PersonInfo>>(),
          sp.GetRequiredService<IAlertService>(),
          sp.GetRequiredService<IBiologicalSexFormatter>(),
          sp.GetRequiredService<IRelationshipTypeFormatter>()))
      .AddTransient<CreateOrUpdatePersonDialogFactory>(sp =>
        person => new CreateOrUpdatePersonDialog(
          person,
          sp.GetRequiredService<ICancellationTokenProvider>(),
          sp.GetRequiredService<IBiologicalSexFormatter>(),
          sp.GetRequiredService<INameTypeFormatter>(),
          sp.GetRequiredService<INameFormatter>(),
          sp.GetRequiredService<IDateFormatter>(),
          sp.GetRequiredService<IComparer<PersonInfo>>(),
          sp.GetRequiredService<IAlertService>(),
          sp.GetRequiredService<DataConverterResolver>(),
          sp.GetRequiredService<SelectNameDialogFactory>(),
          sp.GetRequiredService<SelectRelativesDialogFactory>()));
  }

  public static IServiceProvider Provider =>
    IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("DI container not available.");
}
