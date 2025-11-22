using GT4.Core.Project.Dto;
using GT4.UI.Formatters;

namespace GT4.UI.Items;

public class RelationshipTypeItem : CollectionItemBase<RelationshipType>
{
  private readonly string _Name;
  public RelationshipTypeItem(RelationshipType type, IRelationshipTypeFormatter relationshipTypeFormatter)
    : base(type, string.Empty)
  {
    _Name = relationshipTypeFormatter.ToString(type, null);
  }

  public string Name => _Name;
}
