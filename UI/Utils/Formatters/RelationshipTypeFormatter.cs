using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Utils.Formatters;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  private readonly static Generation _GrandParent = Generation.Parent + Generation.Parent;
  private readonly static Generation _GrandChild = Generation.Child + Generation.Child;

  public string ToString(
    RelationshipType type,
    BiologicalSex? biologicalSex,
    Generation? generationArg,
    Consanguinity? consanguinityArg)
  {
    if (!generationArg.HasValue && !consanguinityArg.HasValue)
    {
      var ret = type switch
      {
        RelationshipType.Parent => GetParent(biologicalSex, Generation.Parent, Consanguinity.Zero),
        RelationshipType.Child => GetChild(biologicalSex, Generation.Child, Consanguinity.Zero),
        RelationshipType.Spouse => GetSpouse(biologicalSex, Generation.Zero, Consanguinity.Zero),
        RelationshipType.AdoptiveParent => GetAdoptiveParent(biologicalSex, Generation.Parent, Consanguinity.Zero),
        RelationshipType.AdoptiveChild => GetAdoptiveChild(biologicalSex, Generation.Child, Consanguinity.Zero),

        _ => throw new NotSupportedException($"type: {type}, sex: {biologicalSex}")
      };

      return ret;
    }
    else if (generationArg.HasValue != consanguinityArg.HasValue)
    {
      throw new ArgumentException("generationArg.HasValue != consanguinityArg.HasValue");
    }

    var generation = generationArg ?? Generation.Zero;
    var consanguinity = consanguinityArg ?? Consanguinity.Zero;
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

  private static string ToLower(string text) => text.ToLower();

  private static string BuildRelationship(string prefix, string main, Generation generation)
  {
    var ret = main;

    if (generation.Value > 4)
    {
      ret = $"{generation.Value}-{ToLower(string.Format(prefix, ret))}";
    }
    else
    {
      while (generation-- > Generation.Zero)
      {
        ret = string.Format(prefix, ToLower(ret));
      }
    }
    return ret;
  }

  private static string BuildRelConsanguinity(BiologicalSex? biologicalSex, string main, Consanguinity consanguinity)
  {
    if (consanguinity <= Consanguinity.Zero)
    {
      throw new ArgumentOutOfRangeException("Argument consanguinity should be > 0");
    }

    var ret = ToLower(main);

    if (Language.Current == Language.RU)
    {
      if (consanguinity > new Consanguinity(3))
      {
        var value = consanguinity.Value + 1;
        ret = biologicalSex switch
        {
          BiologicalSex.Male => string.Format(UIStrings.RelNMale_ru_2, value, ret),
          BiologicalSex.Female => string.Format(UIStrings.RelNFemale_ru_2, value, ret),
          _ => string.Format(UIStrings.RelNUnknown_ru_2, value, ret),
        };
      }
      else if (consanguinity == new Consanguinity(3))
      {
        ret = biologicalSex switch
        {
          BiologicalSex.Male => string.Format(UIStrings.Rel4Male_ru_1, ret),
          BiologicalSex.Female => string.Format(UIStrings.Rel4Female_ru_1, ret),
          _ => string.Format(UIStrings.Rel4Unknown_ru_1, ret),
        };
      }
      else if (consanguinity == new Consanguinity(2))
      {
        ret = biologicalSex switch
        {
          BiologicalSex.Male => string.Format(UIStrings.Rel3Male_ru_1, ret),
          BiologicalSex.Female => string.Format(UIStrings.Rel3Female_ru_1, ret),
          _ => string.Format(UIStrings.Rel3Unknown_ru_1, ret),
        };
      }
      else if (consanguinity == new Consanguinity(1))
      {
        ret = biologicalSex switch
        {
          BiologicalSex.Male => string.Format(UIStrings.Rel2Male_ru_1, ret),
          BiologicalSex.Female => string.Format(UIStrings.Rel2Female_ru_1, ret),
          _ => string.Format(UIStrings.Rel2Unknown_ru_1, ret),
        };
      }

      return ret;
    }

    throw new NotImplementedException();
  }

  private static string GetParent(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (consanguinity != Consanguinity.Zero)
    {
      throw new ArgumentOutOfRangeException($"Argument {nameof(consanguinity)} should be null or Zero");
    }

    if (generation == Generation.Parent)
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

      ret = BuildRelationship(UIStrings.RelGreat_1, ret, generation - _GrandParent);

      return ret;
    }

    throw new ArgumentOutOfRangeException($"Argument {nameof(generation)} should be null or >= 1");
  }

  private static string GetStepParent(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (consanguinity != Consanguinity.Zero)
    {
      throw new ArgumentOutOfRangeException($"Argument {nameof(consanguinity)} should be null or Zero");
    }

    if (generation == Generation.Parent)
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

  private static string GetChild(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (generation > Generation.Child)
    {
      if (consanguinity <= Consanguinity.Zero)
      {
        throw new ArgumentOutOfRangeException("Argument consanguinity should be > 0");
      }

      var ret = GetCousin(biologicalSex, generation, consanguinity);
      return ret;
    }
    else if (generation == Generation.Child)
    {
      if (consanguinity != Consanguinity.Zero)
      {
        throw new NotSupportedException(nameof(consanguinity));
      }

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
      if (consanguinity != Consanguinity.Zero)
      {
        throw new NotSupportedException(nameof(consanguinity));
      }

      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelGrandSon,
        BiologicalSex.Female => UIStrings.RelGrandDaughter,
        _ => UIStrings.RelGrandChild,
      };

      ret = BuildRelationship(UIStrings.RelGreat_1, ret, _GrandChild - generation);
      return ret;
    }

    throw new ArgumentOutOfRangeException("Argument generation should be null or < 0");
  }

  private static string GetStepChild(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (consanguinity != Consanguinity.Zero)
    {
      throw new NotSupportedException(nameof(consanguinity));
    }

    if (generation < Generation.Zero)
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

  private static string GetSpouse(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (consanguinity != Consanguinity.Zero)
    {
      throw new NotSupportedException(nameof(consanguinity));
    }

    if (generation == Generation.Zero)
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

  private static string GetAdoptiveParent(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
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

  private static string GetAdoptiveChild(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
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

  private static string GetSibling(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (generation == Generation.Zero)
    {
      if (consanguinity != Consanguinity.Zero)
      {
        throw new NotSupportedException(nameof(consanguinity));
      }

      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelBrother,
        BiologicalSex.Female => UIStrings.RelSister,
        _ => UIStrings.RelSibling,
      };

      return ret;
    }
    else if (generation == Generation.Parent && consanguinity == Consanguinity.UncleAunt)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelUncle,
        BiologicalSex.Female => UIStrings.RelAunt,
        _ => UIStrings.RelUncleAunt,
      };

      return ret;
    }
    else if (generation > Generation.Parent && generation.Value == consanguinity.Value)
    {
      var ret = GetGrandUncleAunt(biologicalSex, generation);

      return ret;
    }
    else if (generation < Generation.Zero)
    {
      throw new NotSupportedException(nameof(generation));
    }

    throw new ArgumentOutOfRangeException("Argument generation should be null or <= 0");
  }

  private static string GetSiblingByFather(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    var sibling = GetSibling(biologicalSex, generation, consanguinity);
    if (generation == Generation.Zero)
    {
      var ret = string.Format(UIStrings.RelParental_1, sibling);
      return ret;
    }

    return sibling;
  }

  private static string GetSiblingByMother(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    var sibling = GetSibling(biologicalSex, generation, consanguinity);
    if (generation == Generation.Zero)
    {
      var ret = string.Format(UIStrings.RelMaternal_1, sibling);
      return ret;
    }

    return sibling;
  }

  private static string GetAdoptiveSibling(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
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

  private static string GetStepSibling(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
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

  private static string GetGrandUncleAunt(BiologicalSex? biologicalSex, Generation generation)
  {
    if (Language.Current == Language.RU)
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelGrandFather,
        BiologicalSex.Female => UIStrings.RelGrandFather,
        _ => UIStrings.RelGrandParent,
      };

      ret = BuildRelationship(UIStrings.RelGreat_1, ret, generation - _GrandParent);
      ret = BuildRelConsanguinity(biologicalSex, ret, Consanguinity.UncleAunt);
      return ret;
    }
    else
    {
      var ret = biologicalSex switch
      {
        BiologicalSex.Male => UIStrings.RelGrandUncle_en,
        BiologicalSex.Female => UIStrings.RelGrandAunt_en,
        _ => UIStrings.RelGrandUncleAunt_en,
      };

      ret = BuildRelationship(UIStrings.RelGreat_1, ret, generation - _GrandParent);
      return ret;
    }
  }

  private static string GetCousin(BiologicalSex? biologicalSex, Generation generation, Consanguinity consanguinity)
  {
    if (generation.Value >= consanguinity.Value)
    {
      throw new ArgumentException("generation.Value >= consanguinity.Value");
    }

    if (Language.Current == Language.RU)
    {
      var ret = generation.Value switch
      {
        0 => GetSibling(biologicalSex, Generation.Zero, Consanguinity.Zero),
        1 => GetSibling(biologicalSex, Generation.Parent, Consanguinity.UncleAunt),
        _ => GetParent(biologicalSex, generation, Consanguinity.Zero)
      };

      var normalizedConsanguinity = generation.Value switch
      {
        0 => consanguinity,
        1 => consanguinity - Consanguinity.UncleAunt,
        _ => consanguinity - (new Consanguinity(generation.Value) - Consanguinity.UncleAunt)
      };

      ret = BuildRelConsanguinity(biologicalSex, ret, normalizedConsanguinity);

      return ret;
    }
    else
    {
      int cousin_no = consanguinity.Value - generation.Value;
      var ret = cousin_no switch
      {
        1 => UIStrings.RelCousinFirst_en,
        2 => UIStrings.RelCousinSecond_en,
        3 => UIStrings.RelCousinThird_en,
        4 => UIStrings.RelCousinFourth_en,
        _ => string.Format(UIStrings.RelCousinN_1_en, cousin_no)
      };

      ret = generation.Value switch
      {
        0 => ret,
        1 => string.Format("{0} {1}", ret, UIStrings.RelCousin1Removed_en),
        2 => string.Format("{0} {1}", ret, UIStrings.RelCousin2Removed_en),
        _ => string.Format("{0} {1}", ret, string.Format(UIStrings.RelCousinNRemoved_en, generation.Value)),
      };

      return ret;
    }
  }
}