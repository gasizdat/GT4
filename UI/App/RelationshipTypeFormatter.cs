using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string GetRelationshipTypeName(RelationshipType type)
  {
    switch (type)
    {
      case RelationshipType.Mother:
        return UIStrings.RelMother;
      case RelationshipType.Father:
        return UIStrings.RelFather;
      case RelationshipType.AdoptiveMother:
        return string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelMother);
      case RelationshipType.AdoptiveFather:
        return string.Format(UIStrings.RelAdoptiveMale_1, UIStrings.RelFather);
      case RelationshipType.Spouse:
        return UIStrings.RelSpouse;
      case RelationshipType.Wife:
        return UIStrings.RelWife;
      case RelationshipType.Husband:
        return UIStrings.RelHusband;
      case RelationshipType.Son:
        return UIStrings.RelSon;
      case RelationshipType.Daughter:
        return UIStrings.RelDaughter;
      case RelationshipType.Child:
        return UIStrings.RelChild;
      default:
        return UIStrings.RelUnknown;
    }
  }
}