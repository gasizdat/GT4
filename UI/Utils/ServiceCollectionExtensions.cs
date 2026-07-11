using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Comparers;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddUIUtils(this IServiceCollection services)
  {
    return services
      .AddSingleton<IDateFormatter, DateFormatter>()
      .AddSingleton<INameFormatter, NameFormatter>()
      .AddSingleton<IDateSpanFormatter, DateSpanFormatter>()
      .AddSingleton<IRelationshipTypeFormatter, RelationshipTypeFormatter>()
      .AddSingleton<INameTypeFormatter, NameTypeFormatter>()
      .AddSingleton<IBiologicalSexFormatter, BiologicalSexFormatter>()
      .AddSingleton<LanguageSetting>()
      .AddSingleton<FontScale>()
      .AddSingleton<IComparer<ProjectInfo>, ProjectInfoComparer>()
      .AddSingleton<IComparer<PersonInfo>, PersonInfoComparer>()
      .AddSingleton<IComparer<Name>, NameComparer>()
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonPhoto)
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonMainPhoto)
      .AddKeyedSingleton<IDataConverter, TextDataConverter>(DataCategory.PersonBio)
      .AddKeyedSingleton<ISettingEditor, FontScaleSetting>(nameof(FontScaleSetting))
      .AddKeyedSingleton<ISettingEditor>(DateFormatKind.Full, (sp, _) => DateFormatSettingFactory(
        sp, "DateFormatter.FullDateFormat", "DD MM YYYY", UIStrings.FieldDateDisplayFormat, UIStrings.FieldDateDisplayFormatHint, Date.Now))
      .AddKeyedSingleton<ISettingEditor>(DateFormatKind.Short, (sp, _) => DateFormatSettingFactory(
        sp, "DateFormatter.ShortDateFormat", "MM YYYY", UIStrings.FieldShortDateDisplayFormat, UIStrings.FieldShortDateDisplayFormatHint,
        Date.Now with { Status = DateStatus.DayUnknown }))
      .AddKeyedSingleton<ISettingEditor>(NameFormat.CommonPersonName, (sp, _) => PersonNameSettingFactory(
        sp, NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL", UIStrings.FieldCommonPersonNameFormat))
      .AddKeyedSingleton<ISettingEditor>(NameFormat.FullPersonName, (sp, _) => PersonNameSettingFactory(
        sp, NameFormat.FullPersonName, "NameFormatter.FullPersonName", "FF PP LL (FN)", UIStrings.FieldFullPersonNameFormat))
      .AddKeyedSingleton<ISettingEditor>(NameFormat.PersonInitials, (sp, _) => PersonNameSettingFactory(
        sp, NameFormat.PersonInitials, "NameFormatter.PersonInitialsSetting", "LL FF. PP.", UIStrings.FieldPersonInitialsFormat))
      .AddKeyedSingleton<ISettingEditor>(NameFormat.ShortPersonName, (sp, _) => PersonNameSettingFactory(
        sp, NameFormat.ShortPersonName, "NameFormatter.ShortPersonNameSetting", "FF PP", UIStrings.ShortPersonNameFormat))
      .AddKeyedSingleton<IComparer<PersonInfo>>(NameFormat.CommonPersonName, PersonInfoComparerFactory)
      .AddKeyedSingleton<IComparer<PersonInfo>>(NameFormat.FullPersonName, PersonInfoComparerFactory)
      .AddKeyedSingleton<IComparer<PersonInfo>>(NameFormat.ShortPersonName, PersonInfoComparerFactory)
      .AddKeyedSingleton<IComparer<PersonInfo>>(NameFormat.PersonInitials, PersonInfoComparerFactory)
      ;
  }

  private static PersonInfoComparer PersonInfoComparerFactory(IServiceProvider serviceProvider, object? key)
  {
    var nameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    var nameFormat = (NameFormat)(key ?? throw new ArgumentNullException(nameof(key)));
    var ret = new PersonInfoComparer(nameFormatter, nameFormat);

    return ret;
  }

  private static PersonNameSetting PersonNameSettingFactory(
    IServiceProvider serviceProvider, NameFormat nameFormat, string formatSection, string defaultFormat, string displayName)
  {
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var interactiveConfiguration = serviceProvider.GetKeyedService<IInteractiveConfiguration>(WellKnownActiveConfigurations.AppConfig);

    return new PersonNameSetting(serviceProvider, configuration, interactiveConfiguration, nameFormat, formatSection, defaultFormat, displayName);
  }

  private static DateFormatSetting DateFormatSettingFactory(
    IServiceProvider serviceProvider, string formatSection, string defaultFormat, string displayName, string description, Date exampleDate)
  {
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var interactiveConfiguration = serviceProvider.GetKeyedService<IInteractiveConfiguration>(WellKnownActiveConfigurations.AppConfig);

    return new DateFormatSetting(serviceProvider, configuration, interactiveConfiguration, formatSection, defaultFormat, displayName, description, exampleDate);
  }
}
