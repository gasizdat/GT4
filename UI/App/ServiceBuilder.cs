using GT4.Core.Project;
using GT4.Core.Utils;
using GT4.UI.App.Items;
using GT4.UI.Comparers;
using System.Collections.Generic;

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
        .AddSingleton<IComparer<FamilyMemberInfoItem>, FamilyMemberInfoItemComparer>()
        .AddSingleton<IComparer<ProjectItem>, ProjectItemComparer>()
        .AddSingleton<IComparer<PersonInfoItem>, PersonInfoItemComparer>()
        .BuildDefaultUtils()
        .BuildDefaultProject()
        .BuildServiceProvider();

  public static ServiceProvider DefaultServices => _DefaultServices;
}
