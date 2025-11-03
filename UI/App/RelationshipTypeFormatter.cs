using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string GetRelationshipTypeName(RelationshipType type)
  {
    return type switch
    {
      RelationshipType.Mother => UIStrings.RelMother,
      RelationshipType.Father => UIStrings.RelFather,
      RelationshipType.AdoptiveMother => string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelMother),
      RelationshipType.AdoptiveFather => string.Format(UIStrings.RelAdoptiveMale_1, UIStrings.RelFather),
      RelationshipType.Spouse => UIStrings.RelSpouse,
      RelationshipType.Wife => UIStrings.RelWife,
      RelationshipType.Husband => UIStrings.RelHusband,
      RelationshipType.Son => UIStrings.RelSon,
      RelationshipType.Daughter => UIStrings.RelDaughter,
      RelationshipType.Child => UIStrings.RelChild,
      _ => UIStrings.RelUnknown
    };
  }
}