using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Formatters;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string ToString(RelationshipType type, BiologicalSex? biologicalSex = null)
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
        RelationshipType.Sibling => UIStrings.RelBrother,
        RelationshipType.SiblingByFather => string.Format(UIStrings.RelParental_1, UIStrings.RelBrother),
        RelationshipType.SiblingByMother => string.Format(UIStrings.RelMaternal_1, UIStrings.RelBrother),
        RelationshipType.AdoptiveSibling => string.Format(UIStrings.RelAdoptiveMale_1, UIStrings.RelBrother),
        RelationshipType.StepSibling => string.Format(UIStrings.RelStepMale_1, UIStrings.RelBrother),
        RelationshipType.StepParent => UIStrings.RelStepFather,
        RelationshipType.StepChild => UIStrings.RelStepSon,

        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      },
      BiologicalSex.Female => type switch
      {
        RelationshipType.Parent => UIStrings.RelMother,
        RelationshipType.Child => UIStrings.RelDaughter,
        RelationshipType.Spose => UIStrings.RelWife,
        RelationshipType.AdoptiveParent => string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelMother),
        RelationshipType.AdoptiveChild => string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelDaughter),
        RelationshipType.Sibling => UIStrings.RelSister,
        RelationshipType.SiblingByFather => string.Format(UIStrings.RelParental_1, UIStrings.RelSister),
        RelationshipType.SiblingByMother => string.Format(UIStrings.RelMaternal_1, UIStrings.RelSister),
        RelationshipType.AdoptiveSibling => string.Format(UIStrings.RelAdoptiveFemale_1, UIStrings.RelSister),
        RelationshipType.StepSibling => string.Format(UIStrings.RelStepFemale_1, UIStrings.RelSister),
        RelationshipType.StepParent => UIStrings.RelStepMother,
        RelationshipType.StepChild => UIStrings.RelStepDaughter,

        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      },
      _ => type switch
      {
        RelationshipType.Parent => UIStrings.RelParent,
        RelationshipType.Child => UIStrings.RelChild,
        RelationshipType.Spose => UIStrings.RelSpouse,
        RelationshipType.AdoptiveParent => string.Format(UIStrings.RelAdoptiveInvariant_1, UIStrings.RelParent),
        RelationshipType.AdoptiveChild => string.Format(UIStrings.RelAdoptiveInvariant_1, UIStrings.RelChild),
        RelationshipType.Sibling => UIStrings.RelSibling,
        RelationshipType.SiblingByFather => string.Format(UIStrings.RelParental_1, UIStrings.RelSibling),
        RelationshipType.SiblingByMother => string.Format(UIStrings.RelMaternal_1, UIStrings.RelSibling),
        RelationshipType.AdoptiveSibling => string.Format(UIStrings.RelAdoptiveInvariant_1, UIStrings.RelSibling),
        RelationshipType.StepSibling => string.Format(UIStrings.RelStepInvariant_1, UIStrings.RelSibling),
        RelationshipType.StepParent => UIStrings.RelStepParent,
        RelationshipType.StepChild => UIStrings.RelStepChild,

        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      }
    };

    return ret;
  }
}