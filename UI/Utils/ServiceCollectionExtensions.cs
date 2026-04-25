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
      .AddSingleton<IComparer<ProjectInfo>, ProjectInfoComparer>()
      .AddSingleton<IComparer<PersonInfo>, PersonInfoComparer>()
      .AddSingleton<IComparer<Name>, NameComparer>()
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonPhoto)
      .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonMainPhoto)
      .AddKeyedSingleton<IDataConverter, TextDataConverter>(DataCategory.PersonBio)
      .AddKeyedSingleton<ISettingEditor, FullDateFormatSetting>(nameof(FullDateFormatSetting))
      .AddKeyedSingleton<ISettingEditor, ShortDateFormatSetting>(nameof(ShortDateFormatSetting))
      ;
  }
}
