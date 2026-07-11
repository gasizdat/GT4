using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Comparers;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;

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
      .AddKeyedSingleton<ISettingEditor, FontScaleSetting>(SettingKeys.FontScale)
      .AddKeyedSingleton<ISettingEditor, FullDateFormatSetting>(nameof(FullDateFormatSetting))
      .AddKeyedSingleton<ISettingEditor, ShortDateFormatSetting>(nameof(ShortDateFormatSetting))
      .AddKeyedSingleton<ISettingEditor, CommonPersonNameSetting>(NameFormat.CommonPersonName)
      .AddKeyedSingleton<ISettingEditor, FullPersonNameSetting>(NameFormat.FullPersonName)
      .AddKeyedSingleton<ISettingEditor, PersonInitialsSetting>(NameFormat.PersonInitials)
      .AddKeyedSingleton<ISettingEditor, ShortPersonNameSetting>(NameFormat.ShortPersonName)
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
}
