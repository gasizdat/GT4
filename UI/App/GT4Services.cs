using GT4.Core.Gedcom.Extensions;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils.Extensions;
using GT4.UI.Abstraction;
using GT4.UI.Converters;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;
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
      .AddKeyedSingleton<IDataConverter, GedcomDataConverter>(DataCategory.PersonGedcomTags)
      .AddKeyedSingleton<IDataConverter, PhotoTagDataConverter>(DataCategory.PersonMainPhotoTagged)
      .AddKeyedSingleton<IDataConverter, PhotoTagDataConverter>(DataCategory.PersonPhotoTagged)
      .AddKeyedSingleton<IDataConverter, AttachmentDataConverter>(DataCategory.PersonAttachment)
      .AddKeyedSingleton<IDataConverter, AttachmentDataConverter>(DataCategory.FamilyAttachment)
      .AddSingleton<IAlertService, AlertService>()
      .AddSingleton<GedcomImportEncoding>()
      .AddTransient<SelectNameDialog.Factory>()
      .AddTransient<SelectRelativesDialog.Factory>()
      .AddTransient<SelectPersonDialog.Factory>()
      .AddTransient<SelectMediaDialog.Factory>()
      .AddTransient<CreateOrUpdatePersonDialog.Factory>();
  }

  public static IServiceProvider Provider =>
    IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("DI container not available.");
}
