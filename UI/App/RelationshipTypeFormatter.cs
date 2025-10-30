using GT4.Core.Project.Dto;

namespace GT4.UI;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string GetRelationshipTypeName(RelationshipType type)
  {
    switch (type)
    {
      case RelationshipType.Mother:
        return "Mother";
      case RelationshipType.Father:
        return "Father";
      case RelationshipType.AdoptiveMother:
        return "Adoptive Mother";
      case RelationshipType.AdoptiveFather:
        return "Adoptive Father";
      case RelationshipType.Spouse:
        return "Spouse";
      case RelationshipType.Wife:
        return "Wife";
      case RelationshipType.Husband:
        return "Husband";
      case RelationshipType.Son:
        return "Son";
      case RelationshipType.Daughter:
        return "Daughter";
      case RelationshipType.Child:
        return "Child";
      default:
        return "Unknown";
    }
  }
}