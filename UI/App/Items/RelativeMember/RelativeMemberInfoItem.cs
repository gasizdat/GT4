using GT4.Core.Project.Dto;
using GT4.UI.Formatters;

namespace GT4.UI.Items;

public class RelativeMemberInfoItem : PersonInfoItem
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly RelativeInfo _RelativeInfo;

  public RelativeMemberInfoItem(
    RelativeInfo relativeInfo, 
    IDateFormatter dateFormatter, 
    IRelationshipTypeFormatter relationshipTypeFormatter,
    INameFormatter nameFormatter)
    : base(personInfo: relativeInfo, nameFormatter: nameFormatter)
  {
    _DateFormatter = dateFormatter;
    _RelationshipTypeFormatter = relationshipTypeFormatter;
    _RelativeInfo = relativeInfo;
  }

  public bool ShowDate => _RelativeInfo.Relative.Date.HasValue;
  public string Date => _DateFormatter.ToString(_RelativeInfo.Relative.Date);
  public string RelationTypeName => _RelationshipTypeFormatter.GetRelationshipTypeName(_RelativeInfo.Relative.Type, _RelativeInfo.BiologicalSex);
  public RelativeInfo RelativeInfo => _RelativeInfo;
}
