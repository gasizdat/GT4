using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;

namespace GT4.UI.Items;

public class RelativeMemberInfoItem : PersonInfoItem
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly Date _PersonBirthDate;
  private readonly RelativeInfo _RelativeInfo;

  private Date? _RelationshipDate => _RelativeInfo.Type switch
  {
    RelationshipType.Parent => _PersonBirthDate,
    RelationshipType.Child => _RelativeInfo.BirthDate,
    _ => _RelativeInfo.Date
  };

  public RelativeMemberInfoItem(
    Date personBirthDate,
    RelativeInfo relativeInfo,
    IDateFormatter dateFormatter,
    IRelationshipTypeFormatter relationshipTypeFormatter,
    INameFormatter nameFormatter)
    : base(personInfo: relativeInfo, nameFormatter: nameFormatter)
  {
    _PersonBirthDate = personBirthDate;
    _DateFormatter = dateFormatter;
    _RelationshipTypeFormatter = relationshipTypeFormatter;
    _RelativeInfo = relativeInfo;
  }

  public bool ShowDate => _RelationshipDate.HasValue;
  public string Date => _DateFormatter.ToString(_RelationshipDate);
  public string RelationTypeName =>
    _RelationshipTypeFormatter.GetRelationshipTypeName(_RelativeInfo.Type, _RelativeInfo.BiologicalSex);
  public RelativeInfo RelativeInfo => _RelativeInfo;
}
