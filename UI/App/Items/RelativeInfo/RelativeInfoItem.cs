using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;

namespace GT4.UI.Items;

public class RelativeInfoItem : PersonInfoItem
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

  public RelativeInfoItem(
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

  public bool ShowDate =>
    _RelationshipDate.HasValue &&
    _RelationshipDate.Value.Status != DateStatus.Unknown &&
    _RelativeInfo.Type switch
    {
      RelationshipType.Spose => true,
      RelationshipType.AdoptiveChild => true,
      RelationshipType.StepChild => true,
      RelationshipType.AdoptiveParent => true,
      RelationshipType.StepParent => true,
      RelationshipType.AdoptiveSibling => true,
      RelationshipType.StepSibling => true,
      _ => false
    };
  public string Date => _DateFormatter.ToString(_RelationshipDate);
  public string RelationTypeName =>
    _RelationshipTypeFormatter.ToString(_RelativeInfo.Type, _RelativeInfo.BiologicalSex);
  public RelativeInfo RelativeInfo => _RelativeInfo;
}
