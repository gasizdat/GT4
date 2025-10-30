using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class RelativeMemberInfoItem : CollectionItemBase<Relative>
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly PersonInfoItem _PersonItem;

  public RelativeMemberInfoItem(Relative relative, ServiceProvider services)
    : base(relative, "wife.png")
  {
    _DateFormatter = services.GetRequiredService<IDateFormatter>();
    _RelationshipTypeFormatter = services.GetRequiredService<IRelationshipTypeFormatter>();
    _PersonItem = new PersonInfoItem(relative.Person, services.GetRequiredService<INameFormatter>());
  }

  public bool ShowDate => Info.Date.HasValue;
  public string Date => _DateFormatter.ToString(Info.Date);
  public string RelationTypeName => _RelationshipTypeFormatter.GetRelationshipTypeName(Info.Type);
  public PersonInfoItem PersonItem => _PersonItem;
}
