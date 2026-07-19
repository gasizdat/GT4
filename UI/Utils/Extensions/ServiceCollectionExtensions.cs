using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Comparers;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.UI.Utils.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddUIUtils(this IServiceCollection services)
  {
    return services
      .AddHttpClient()
      .AddSingleton<IDateFormatter, DateFormatter>()
      .AddSingleton<INameFormatter, NameFormatter>()
      .AddSingleton<IDateSpanFormatter, DateSpanFormatter>()
      .AddSingleton<IRelationshipTypeFormatter, RelationshipTypeFormatter>()
      .AddSingleton<INameTypeFormatter, NameTypeFormatter>()
      .AddSingleton<IBiologicalSexFormatter, BiologicalSexFormatter>()
      .AddSingleton<LanguageSetting>()
      .AddSingleton<FontScale>()
      .AddSingleton<BackgroundAnimation>()
      .AddSingleton<IComparer<ProjectInfo>, ProjectInfoComparer>()
      .AddSingleton<IComparer<PersonInfo>, PersonInfoComparer>()
      .AddSingleton<IComparer<Name>, NameComparer>()
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonPhoto)
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonMainPhoto)
      .AddKeyedSingleton<IDataConverter, TextDataConverter>(DataCategory.PersonBio)
      .AddKeyedSingleton<ISettingEditor, FontScaleSetting>(SettingKeys.FontScale)
      .AddKeyedSingleton<ISettingEditor, BackgroundAnimationSetting>(SettingKeys.BackgroundAnimation)
      .AddKeyedSingleton<ISettingEditor, DateFormatSetting>(DateFormatKind.Full)
      .AddKeyedSingleton<ISettingEditor, DateFormatSetting>(DateFormatKind.Short)
      .AddKeyedSingleton<ISettingEditor, DateSpanFormatSetting>(DateSpanFormatKind.Full)
      .AddKeyedSingleton<ISettingEditor, DateSpanFormatSetting>(DateSpanFormatKind.Short)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.CommonPersonName)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.FullPersonName)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.PersonInitials)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.ShortPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.CommonPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.FullPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.ShortPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.PersonInitials)
      // Lazy resolvers, not direct constructor injection: DateFormatSetting/DateSpanFormatSetting/
      // PersonNameSetting need the matching formatter only inside their Example getter, and the
      // formatters themselves depend on these same keyed settings -- a direct dependency would be a
      // construction-time cycle.
      .AddTransient<DateFormatterResolver>(sp => sp.GetRequiredService<IDateFormatter>)
      .AddTransient<DateSpanFormatterResolver>(sp => sp.GetRequiredService<IDateSpanFormatter>)
      .AddTransient<NameFormatterResolver>(sp => sp.GetRequiredService<INameFormatter>)
      .AddTransient<SettingEditorsResolver>(sp => () => sp.GetKeyedServices<ISettingEditor>(KeyedService.AnyKey))
      ;
  }
}
