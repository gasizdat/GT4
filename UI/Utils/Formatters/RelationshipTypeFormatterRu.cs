using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Utils.Formatters.Detailed;

using Row = RelationshipTypeTableRow;
using S = UIStrings;
using Table = Dictionary<RelationshipType, RelationshipTypeTableRow>;

internal class RelationshipTypeFormatterRu : RelationshipTypeFormatterBase
{
  private readonly static Generation _GreatnessStartLevel = new Generation(2);
  private readonly static Generation _GreatnessMaxLevel = new Generation(4);
  private readonly static Consanguinity _ConsanguinityMaxLevel = new Consanguinity(4);

  public RelationshipTypeFormatterRu(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
    : base(type, biologicalSex, generation, consanguinity)
  {
  }

  protected string AddGreatness(string main)
  {
    var ret = main;
    var generation = AbsGen - _GreatnessStartLevel;

    if (generation > _GreatnessMaxLevel)
    {
      ret = $"{generation.Value}-{string.Format(S.RelGreat_1, ret)}";
    }
    else
    {
      while (generation-- > Generation.Zero)
      {
        ret = string.Format(S.RelGreat_1, ret);
      }
    }
    return ret;
  }

  protected string AddConsanguinity(string main, Consanguinity consanguinity)
  {
    var ret = main;
    var maxLevel = _ConsanguinityMaxLevel;
    Row row;

    if (consanguinity > maxLevel)
    {
      row = new Row(F: S.RelNFemale_2, M: S.RelNMale_2, U: S.RelNUnknown_2);
      ret = string.Format(row.ToString(Sex), consanguinity.Value, ret);

      return ret;
    }

    if (consanguinity == maxLevel--)
    {
      row = new Row(F: S.Rel4Female_1, M: S.Rel4Male_1, U: S.Rel4Unknown_1);
    }
    else if (consanguinity == maxLevel--)
    {
      row = new Row(F: S.Rel3Female_1, M: S.Rel3Male_1, U: S.Rel3Unknown_1);
    }
    else if (consanguinity == maxLevel--)
    {
      row = new Row(F: S.Rel2Female_1, M: S.Rel2Male_1, U: S.Rel2Unknown_1);
    }
    else
    {
      return ret;
    }

    ret = string.Format(row.ToString(Sex), ret);
    return ret;
  }

  protected string AddAncestorConsanguinity(string main)
  {
    var consanguinityStartLevel = new Consanguinity(Gen.Value);
    if (Gen > Generation.Parent)
    {
      // There is no any additional consaguinities between "дед" and "двоюродный дед"
      --consanguinityStartLevel;
    }
    var consanguinity = Con - consanguinityStartLevel;
    var ret = AddConsanguinity(main, consanguinity);

    return ret;
  }

  protected string D0_C0()
  {
    var table = new Table
    {
      [RelationshipType.Parent] = new(F: S.RelMother, M: S.RelFather, U: S.RelParent),
      [RelationshipType.Child] = new(F: S.RelDaughter, M: S.RelSon, U: S.RelChild),
      [RelationshipType.Spouse] = new(F: S.RelWife, M: S.RelHusband, U: S.RelSpouse),
      [RelationshipType.Sibling] = new(F: S.RelSister, M: S.RelBrother, U: S.RelSibling),
      [RelationshipType.StepParent] = new(F: S.RelStepMother, M: S.RelStepFather, U: S.RelStepParent),
      [RelationshipType.StepChild] = new(F: S.RelStepDaughter, M: S.RelStepSon, U: S.RelStepChild),
      [RelationshipType.AdoptiveParent] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Parent),
      [RelationshipType.AdoptiveChild] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Child),
      [RelationshipType.AdoptiveSibling] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Sibling),
      [RelationshipType.SiblingByFather] = new(F: S.RelParental_1, M: S.RelParental_1, U: S.RelParental_1, RelationshipType.Sibling),
      [RelationshipType.SiblingByMother] = new(F: S.RelMaternal_1, M: S.RelMaternal_1, U: S.RelMaternal_1, RelationshipType.Sibling),
      [RelationshipType.StepSibling] = new(F: S.RelStepMale_1, M: S.RelStepFemale_1, U: S.RelStepInvariant_1, RelationshipType.Sibling),
    };

    var ret = ToString(table);

    return ret;
  }

  protected string D0_CM()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelSister, M: S.RelBrother, U: S.RelSibling),
      [RelationshipType.Sibling] = new(RelationshipType.Child),
      [RelationshipType.AdoptiveSibling] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Sibling),
      [RelationshipType.SiblingByFather] = new(F: S.RelParental_1, M: S.RelParental_1, U: S.RelParental_1, RelationshipType.Sibling),
      [RelationshipType.SiblingByMother] = new(F: S.RelMaternal_1, M: S.RelMaternal_1, U: S.RelMaternal_1, RelationshipType.Sibling),
      [RelationshipType.StepSibling] = new(F: S.RelStepFemale_1, M: S.RelStepMale_1, U: S.RelStepInvariant_1, RelationshipType.Sibling),
    };

    var ret = ToString(table);
    ret = AddConsanguinity(ret, Con);

    return ret;
  }

  protected string D1_C0()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelDaughter, M: S.RelSon, U: S.RelChild),
      [RelationshipType.StepChild] = new(F: S.RelStepDaughter, M: S.RelStepSon, U: S.RelStepChild),
      [RelationshipType.AdoptiveChild] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Child),
      [RelationshipType.Spouse] = new(F: S.RelDaughterInLaw, M: S.RelSonInLaw, U: S.RelChildInLaw),
    };

    var ret = ToString(table);

    return ret;
  }

  protected string D1_CM()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelNiece, M: S.RelNephew, U: S.RelNephewNiece),
      [RelationshipType.AdoptiveChild] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Child),
    };

    var ret = ToString(table);
    ret = AddConsanguinity(ret, Con);

    return ret;
  }

  protected string DN_C0()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelGrandDaughter, M: S.RelGrandSon, U: S.RelGrandChild),
      [RelationshipType.AdoptiveChild] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Child),
    };

    var ret = ToString(table);
    ret = AddGreatness(ret);

    return ret;
  }

  protected string DN_CM()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelGrandDaughter, M: S.RelGrandSon, U: S.RelGrandChild),
      [RelationshipType.AdoptiveChild] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Child),
    };

    var ret = ToString(table);
    ret = AddGreatness(ret);
    ret = AddConsanguinity(ret, Con + Consanguinity.Sibling);

    return ret;
  }

  protected string A1_C0()
  {
    var table = new Table
    {
      [RelationshipType.Parent] = new(F: S.RelMother, M: S.RelFather, U: S.RelParent),
      [RelationshipType.SpouseParent] = new(F: S.RelInLawFemale, M: S.RelInLawMale, U: S.RelInLawUnknown),
      [RelationshipType.HusbandParent] = new(F: S.RelHusbandsMother, M: S.RelHusbandsFather, U: S.RelInLawUnknown),
      [RelationshipType.WifeParent] = new(F: S.RelWifesMother, M: S.RelWifesFather, U: S.RelInLawUnknown),
      [RelationshipType.StepParent] = new(F: S.RelStepMother, M: S.RelStepFather, U: S.RelStepParent),
      [RelationshipType.AdoptiveParent] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Parent),
    };

    var ret = ToString(table);

    return ret;
  }

  protected string A1_CM()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelAunt, M: S.RelUncle, U: S.RelUncleAunt),
      [RelationshipType.Sibling] = new(RelationshipType.Child),
      [RelationshipType.Spouse] = new(RelationshipType.Child),
    };

    var ret = ToString(table);
    ret = AddAncestorConsanguinity(ret);

    return ret;
  }

  protected string AN_C0()
  {
    var table = new Table
    {
      [RelationshipType.Parent] = new(F: S.RelGrandMother, M: S.RelGrandFather, U: S.RelGrandParent),
      [RelationshipType.AdoptiveParent] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Parent),
    };

    var ret = ToString(table);
    ret = AddGreatness(ret);

    return ret;
  }

  protected string AN_CM()
  {
    if (Gen.Value >= Con.Value)
    {
      Guard();
    }

    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelGrandMother, M: S.RelGrandFather, U: S.RelGrandParent),
      [RelationshipType.Sibling] = new(RelationshipType.Child),
      [RelationshipType.AdoptiveSibling] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Sibling),
      [RelationshipType.Spouse] = new(RelationshipType.Child),
    };

    var ret = ToString(table);
    ret = AddGreatness(ret);
    ret = AddAncestorConsanguinity(ret);

    return ret;
  }

  protected override Func<string>[][] GetConverters()
  {
    if (Gen > Generation.Zero) //ancestors
    {
      return
      [
        [Guard],
        [A1_C0, A1_CM],
        [AN_C0, AN_CM]
      ];
    }
    else //descendants
    {
      return
      [
        [D0_C0, D0_CM],
        [D1_C0, D1_CM],
        [DN_C0, DN_CM],
      ];
    }
  }
}
