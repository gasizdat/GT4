using GT4.Core.Project.Dto;

namespace GT4.UI.Items;

public class RelativeMemberInfoItem : CollectionItemBase<Relative>
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;

  public RelativeMemberInfoItem(Relative relative, IServiceProvider serviceProvider)
    : base(relative, "wife.png")
  {
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _RelationshipTypeFormatter = serviceProvider.GetRequiredService<IRelationshipTypeFormatter>();
  }

  public bool ShowDate => Info.Date.HasValue;
  public string Date => _DateFormatter.ToString(Info.Date);
  public string RelationTypeName => _RelationshipTypeFormatter.GetRelationshipTypeName(Info.Type);
}
