using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using System.Diagnostics.CodeAnalysis;

namespace GT4.UI.Utils.Formatters.Detailed;

using Converters = Func<string>[][];
using Row = RelationshipTypeTableRow;
using S = UIStrings;
using Table = Dictionary<RelationshipType, RelationshipTypeTableRow>;

internal class RelationshipTypeFormatterRu
{
  private readonly RelationshipType _Type;
  private readonly BiologicalSex _Sex;
  private readonly Generation _Gen;
  private readonly Consanguinity _Con;
  private readonly Converters _Converters;
  private readonly static Generation _GreatnessStartLevel = new Generation(2);
  private readonly static Generation _GreatnessMaxLevel = new Generation(4);
  private readonly static Consanguinity _ConsanguinityMaxLevel = new Consanguinity(4);

  public RelationshipTypeFormatterRu(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    _Type = type;
    _Sex = biologicalSex ?? BiologicalSex.Unknown;
    _Gen = generation ?? Generation.Zero;
    _Con = consanguinity ?? Consanguinity.Zero;

    if (_Gen > Generation.Zero) //ancestors
    {
      _Converters =
      [
        [Guard],
        [A1_C0, A1_CM],
        [AN_C0, AN_CM]
      ];
    }
    else //descendants
    {
      _Gen = Generation.Zero - _Gen;
      _Converters =
      [
        [D0_C0, D0_CM],
        [D1_C0],
        [DN_C0],
      ];
    }
  }

  public override string ToString()
  {
    try
    {
      var toString = GetConverter();
      var ret = toString();
      ret = ret.Substring(0, 1) + ret.Substring(1).ToLower();

      return ret;
    }
    catch (Exception ex)
    {
      return ex.Message;
    }
  }

  protected Func<string> GetConverter()
  {
    var gen = Math.Abs(_Gen.Value);
    var candidates = _Converters.Length > gen ? _Converters[gen] : _Converters.Last();
    var ret = candidates.Length > _Con.Value ? candidates[_Con.Value] : candidates.Last();

    return ret;
  }

  protected string ToString(Table table, RelationshipType? type = null)
  {
    if (!table.TryGetValue(type ?? _Type, out var row))
    {
      Guard();
    }

    var ret = row.ToString(_Sex);
    if (ret == string.Empty)
    {
      if (!row.SubType.HasValue)
      {
        Guard();
      }
      ret = ToString(table, row.SubType.Value);
    }
    else if (row.SubType.HasValue)
    {
      ret = string.Format(ret, ToString(table, row.SubType.Value));
    }

    return ret;
  }

  protected string AddGreatness(string main)
  {
    var ret = main;
    var generation = _Gen - _GreatnessStartLevel;

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
      row = new Row(F: S.RelNFemale_ru_2, M: S.RelNMale_ru_2, U: S.RelNUnknown_ru_2);
      ret = string.Format(row.ToString(_Sex), consanguinity.Value, ret);

      return ret;
    }

    if (consanguinity == maxLevel--)
    {
      row = new Row(F: S.Rel4Female_ru_1, M: S.Rel4Male_ru_1, U: S.Rel4Unknown_ru_1);
    }
    else if (consanguinity == maxLevel--)
    {
      row = new Row(F: S.Rel3Female_ru_1, M: S.Rel3Male_ru_1, U: S.Rel3Unknown_ru_1);
    }
    else if (consanguinity == maxLevel--)
    {
      row = new Row(F: S.Rel2Female_ru_1, M: S.Rel2Male_ru_1, U: S.Rel2Unknown_ru_1);
    }
    else
    {
      return ret;
    }

    ret = string.Format(row.ToString(_Sex), ret);
    return ret;
  }

  protected string AddAncestorConsanguinity(string main)
  {
    var consanguinityStartLevel = new Consanguinity(_Gen.Value);
    if (_Gen > Generation.Parent)
    {
      // There is no any additional consaguinities between "дед" and "двоюродный дед"
      --consanguinityStartLevel;
    }
    var consanguinity = _Con - consanguinityStartLevel;
    var ret = AddConsanguinity(main, consanguinity);

    return ret;
  }

  [DoesNotReturn]
  protected string Guard()
  {
    throw new ArgumentException($"Unsupported or wrong relationship: Type={_Type}, Sex={_Sex}, G{_Gen.Value}, C{_Con.Value}");
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
      [RelationshipType.SiblingByFather] = new(F: S.RelParental_1, M: S.RelParental_1, U: S.RelParental_1, RelationshipType.Sibling),
      [RelationshipType.SiblingByMother] = new(F: S.RelMaternal_1, M: S.RelMaternal_1, U: S.RelMaternal_1, RelationshipType.Sibling),
    };

    var ret = ToString(table);
    ret = AddConsanguinity(ret, _Con);

    return ret;
  }

  protected string D1_C0()
  {
    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelDaughter, M: S.RelSon, U: S.RelChild),
      [RelationshipType.StepChild] = new(F: S.RelStepDaughter, M: S.RelStepSon, U: S.RelStepChild),
      [RelationshipType.AdoptiveChild] = new(F: S.RelAdoptiveFemale_1, M: S.RelAdoptiveMale_1, U: S.RelAdoptiveInvariant_1, RelationshipType.Child),
    };

    var ret = ToString(table);

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

  protected string A1_C0()
  {
    var table = new Table
    {
      [RelationshipType.Parent] = new(F: S.RelMother, M: S.RelFather, U: S.RelParent),
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
    if (_Gen.Value >= _Con.Value)
    {
      Guard();
    }

    var table = new Table
    {
      [RelationshipType.Child] = new(F: S.RelGrandMother, M: S.RelGrandFather, U: S.RelGrandParent),
      [RelationshipType.Sibling] = new(RelationshipType.Child),
      [RelationshipType.Spouse] = new(RelationshipType.Child),
    };

    var ret = ToString(table);
    ret = AddGreatness(ret);
    ret = AddAncestorConsanguinity(ret);

    return ret;
  }
}
