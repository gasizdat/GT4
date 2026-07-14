using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Comparers;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.UI.Utils;

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
      .AddSingleton<IComparer<ProjectInfo>, ProjectInfoComparer>()
      .AddSingleton<IComparer<PersonInfo>, PersonInfoComparer>()
      .AddSingleton<IComparer<Name>, NameComparer>()
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonPhoto)
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonMainPhoto)
      .AddKeyedSingleton<IDataConverter, TextDataConverter>(DataCategory.PersonBio)
      .AddKeyedSingleton<ISettingEditor, FontScaleSetting>(SettingKeys.FontScale)
      .AddKeyedSingleton<ISettingEditor, DateFormatSetting>(DateFormatKind.Full)
      .AddKeyedSingleton<ISettingEditor, DateFormatSetting>(DateFormatKind.Short)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.CommonPersonName)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.FullPersonName)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.PersonInitials)
      .AddKeyedSingleton<ISettingEditor, PersonNameSetting>(NameFormat.ShortPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.CommonPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.FullPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.ShortPersonName)
      .AddKeyedSingleton<IComparer<PersonInfo>, PersonInfoComparer>(NameFormat.PersonInitials)
      ;
  }
}
