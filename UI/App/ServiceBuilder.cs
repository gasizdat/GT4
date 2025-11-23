using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Comparers;
using GT4.UI.Converters;
using GT4.UI.Formatters;
using GT4.UI.Items;

namespace GT4.UI;

public class ServiceBuilder
{
  private static readonly ServiceProvider _DefaultServices = new ServiceCollection()
        .AddSingleton<IDateFormatter, DateFormatter>()
        .AddSingleton<INameFormatter, NameFormatter>()
        .AddSingleton<IDateSpanFormatter, DateSpanFormatter>()
        .AddSingleton<IRelationshipTypeFormatter, RelationshipTypeFormatter>()
        .AddSingleton<INameTypeFormatter, NameTypeFormatter>()
        .AddSingleton<IBiologicalSexFormatter, BiologicalSexFormatter>()
        .AddSingleton<IComparer<FamilyInfoItem>, FamilyInfoItemComparer>()
        .AddSingleton<IComparer<ProjectItem>, ProjectItemComparer>()
        .AddSingleton<IComparer<PersonInfoItem>, PersonInfoItemComparer>()
        .AddSingleton<IComparer<NameInfoItem>, NameInfoItemComparer>()
        .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonPhoto)
        .AddKeyedSingleton<IDataConverter, ImageDataConverter>(DataCategory.PersonMainPhoto)
        .AddKeyedSingleton<IDataConverter, TextDataConverter>(DataCategory.PersonBio)
        .BuildDefaultUtils()
        .BuildDefaultProject()
        .BuildServiceProvider();

  public static ServiceProvider DefaultServices => _DefaultServices;
}
