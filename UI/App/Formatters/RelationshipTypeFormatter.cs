using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Formatters;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string GetRelationshipTypeName(RelationshipType type, BiologicalSex? biologicalSex = null)
  {
    var ret = biologicalSex switch
    {
      BiologicalSex.Male => type switch
      {
        RelationshipType.Parent => UIStrings.RelFather,
        RelationshipType.Child => UIStrings.RelSon,
        RelationshipType.Spose => UIStrings.RelHusband,
        RelationshipType.AdoptiveParent => string.Format(UIStrings.RelAdoptiveMale_1, UIStrings.RelFather),
        RelationshipType.AdoptiveChild => string.Format(UIStrings.RelAdoptiveMale_1, UIStrings.RelSon),
        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      },
      BiologicalSex.Female => type switch
      {
        RelationshipType.Parent => UIStrings.RelMother,
        RelationshipType.Child => UIStrings.RelDaughter,
        RelationshipType.Spose => UIStrings.RelWife,
        RelationshipType.AdoptiveParent => string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelMother),
        RelationshipType.AdoptiveChild => string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelDaughter),
        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      },
      _=> type switch
      {
        RelationshipType.Parent => UIStrings.RelParent,
        RelationshipType.Child => UIStrings.RelChild,
        RelationshipType.Spose => UIStrings.RelSpouse,
        RelationshipType.AdoptiveParent => string.Format(UIStrings.RelAdoptiveInvariant_1, UIStrings.RelParent),
        RelationshipType.AdoptiveChild => string.Format(UIStrings.RelAdoptiveInvariant_1, UIStrings.RelChild),
        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      }
    };

    return ret;
  }
}