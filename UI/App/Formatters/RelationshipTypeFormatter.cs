using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Formatters;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string ToString(
    RelationshipType type,
    BiologicalSex? biologicalSex,
    Generation? generation,
    Consanguinity? consanguinity)
  {
    try
    {
      var ret = type switch
      {
        RelationshipType.Parent => GetParent(biologicalSex, generation, consanguinity),
        RelationshipType.Child => GetChild(biologicalSex, generation, consanguinity),
        RelationshipType.Spouse => GetSpouse(biologicalSex, generation, consanguinity),
        RelationshipType.AdoptiveParent => GetAdoptiveParent(biologicalSex, generation, consanguinity),
        RelationshipType.AdoptiveChild => GetAdoptiveChild(biologicalSex, generation, consanguinity),
        RelationshipType.Sibling => GetSibling(biologicalSex, generation, consanguinity),
        RelationshipType.SiblingByFather => GetSiblingByFather(biologicalSex, generation, consanguinity),
        RelationshipType.SiblingByMother => GetSiblingByMother(biologicalSex, generation, consanguinity),
        RelationshipType.AdoptiveSibling => GetAdoptiveSibling(biologicalSex, generation, consanguinity),
        RelationshipType.StepSibling => GetStepSibling(biologicalSex, generation, consanguinity),
        RelationshipType.StepParent => GetStepParent(biologicalSex, generation, consanguinity),
        RelationshipType.StepChild => GetStepChild(biologicalSex, generation, consanguinity),

        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      };

      return ret;
    }
    catch (Exception)
    {
      return $"Unsupported or wrong relationship: Type={type}, {generation}, {consanguinity}";
    }
  }

  private static string GetParent(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (consanguinity is not null && consanguinity != Consanguinity.Zero)
    {
      throw new ArgumentOutOfRangeException($"Argument {nameof(consanguinity)} should be null or Zero");
    }

    if (generation is null || generation == Generation.Parent)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelFather,
        BiologicalSex.Female => UIStrings.RelMother,
        _ => UIStrings.RelParent,
      };

      return ret;
    }
    else if (generation > Generation.Parent)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelGrandFather,
        BiologicalSex.Female => UIStrings.RelGrandMother,
        _ => UIStrings.RelGrandParent,
      };

      for (var grandness = generation.Value - Generation.Parent; grandness > Generation.Parent; --grandness)
      {
        ret = string.Format(UIStrings.RelGrand_1, ret);
      }

      return ret;
    }

    throw new ArgumentOutOfRangeException($"Argument {nameof(generation)} should be null or >= 1");
  }

  private static string GetStepParent(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (consanguinity is not null && consanguinity != Consanguinity.Zero)
    {
      throw new ArgumentOutOfRangeException($"Argument {nameof(consanguinity)} should be null or Zero");
    }

    if (generation is null || generation == Generation.Parent)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelStepFather,
        BiologicalSex.Female => UIStrings.RelStepMother,
        _ => UIStrings.RelStepParent,
      };

      return ret;
    }
    else if (generation > Generation.Parent)
    {
      throw new NotSupportedException(nameof(generation));
    }

    throw new ArgumentOutOfRangeException($"Argument {nameof(generation)} should be null or = 1");
  }

  private static string GetChild(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (consanguinity is not null && consanguinity != Consanguinity.Zero)
    {
      throw new NotSupportedException(nameof(consanguinity));
    }

    if (generation is null || generation == Generation.Child)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelSon,
        BiologicalSex.Female => UIStrings.RelDaughter,
        _ => UIStrings.RelChild,
      };

      return ret;
    }
    else if (generation < Generation.Child)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelGrandSon,
        BiologicalSex.Female => UIStrings.RelGrandDaughter,
        _ => UIStrings.RelGrandChild,
      };

      for (var grandness = generation.Value - Generation.Child; grandness < Generation.Child; ++grandness)
      {
        ret = string.Format(UIStrings.RelGrand_1, ret);
      }

      return ret;
    }

    throw new ArgumentOutOfRangeException("Argument generation should be null or < 0");
  }
  
  private static string GetStepChild(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (consanguinity is not null && consanguinity != Consanguinity.Zero)
    {
      throw new NotSupportedException(nameof(consanguinity));
    }

    if (generation is null || generation < Generation.Zero)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelStepSon,
        BiologicalSex.Female => UIStrings.RelStepDaughter,
        _ => UIStrings.RelStepChild,
      };

      return ret;
    }

    throw new ArgumentOutOfRangeException("Argument generation should be null or < 0");
  }

  private static string GetSpouse(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (consanguinity is not null && consanguinity != Consanguinity.Zero)
    {
      throw new NotSupportedException(nameof(consanguinity));
    }

    if (generation is null || generation == Generation.Zero)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelHusband,
        BiologicalSex.Female => UIStrings.RelWife,
        _ => UIStrings.RelSpouse,
      };

      return ret;
    }
    else if (generation < Generation.Zero)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelSonInLaw,
        BiologicalSex.Female => UIStrings.RelDaughterInLaw,
        _ => UIStrings.RelChildInLaw,
      };

      return ret;
    }

    throw new ArgumentOutOfRangeException("Argument generation should be null or <= 0");
  }

  private static string GetAdoptiveParent(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var parent = GetParent(biologicalSex, generation, consanguinity);
    var ret = biologicalSex switch
    {
      BiologicalSex.Male => string.Format(UIStrings.RelAdoptiveMale_1, parent),
      BiologicalSex.Female => string.Format(UIStrings.RelAdoptiveFemale_1, parent),
      _ => string.Format(UIStrings.RelAdoptiveInvariant_1, parent),
    };

    return ret;
  }

  private static string GetAdoptiveChild(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var child = GetChild(biologicalSex, generation, consanguinity);
    var ret = biologicalSex switch
    {
      BiologicalSex.Male => string.Format(UIStrings.RelAdoptiveMale_1, child),
      BiologicalSex.Female => string.Format(UIStrings.RelAdoptiveFemale_1, child),
      _ => string.Format(UIStrings.RelAdoptiveInvariant_1, child),
    };

    return ret;
  }

  private static string GetSibling(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (consanguinity is not null && consanguinity != Consanguinity.Zero)
    {
      throw new NotSupportedException(nameof(consanguinity));
    }

    if (generation is null || generation == Generation.Zero)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelBrother,
        BiologicalSex.Female => UIStrings.RelSister,
        _ => UIStrings.RelSibling,
      };

      return ret;
    }
    else if (generation  < Generation.Zero)
    {
      throw new NotSupportedException(nameof(generation));
    }

    throw new ArgumentOutOfRangeException("Argument generation should be null or <= 0");
  }

  private static string GetSiblingByFather(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var sibling = GetSibling(biologicalSex, generation, consanguinity);
    if (generation is null || generation == Generation.Zero)
    {
      var ret = string.Format(UIStrings.RelParental_1, sibling);
      return ret;
    }

    return sibling;
  }

  private static string GetSiblingByMother(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var sibling = GetSibling(biologicalSex, generation, consanguinity);
    if (generation is null || generation == Generation.Zero)
    {
      var ret = string.Format(UIStrings.RelMaternal_1, sibling);
      return ret;
    }

    return sibling;
  }

  private static string GetAdoptiveSibling(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var sibling = GetSibling(biologicalSex, generation, consanguinity);
    var ret = biologicalSex switch
    {
      BiologicalSex.Male => string.Format(UIStrings.RelAdoptiveMale_1, sibling),
      BiologicalSex.Female => string.Format(UIStrings.RelAdoptiveFemale_1, sibling),
      _ => string.Format(UIStrings.RelAdoptiveInvariant_1, sibling),
    };

    return ret;
  }

  private static string GetStepSibling(BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var sibling = GetSibling(biologicalSex, generation, consanguinity);
    var ret = biologicalSex switch
    {
      BiologicalSex.Male => string.Format(UIStrings.RelStepMale_1, sibling),
      BiologicalSex.Female => string.Format(UIStrings.RelStepFemale_1, sibling),
      _ => string.Format(UIStrings.RelStepInvariant_1, sibling),
    };

    return ret;
  }
}