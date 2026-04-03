using GT4.Core.Project.Dto;

namespace GT4.UI.Utils;

partial class NameDeclension
{
  public static string ToFemaleDeclension(Language language, NameType nameType, string name)
  {
    var noDeclensionNameType = nameType & NameType.NoDeclension;
    var ret = noDeclensionNameType switch
    {
      NameType.FirstName => ToFemalePatronymic(language, name),
      NameType.FamilyName => ToFemaleLastName(language, name),
      _ => name
    };
    return ret;
  }

  private static string ToFemalePatronymic(Language language, string firstName)
  {
    if (language == Language.RU)
    {
      return ToFemalePatronymicRU(firstName);
    }
    if (language == Language.EN)
    {
      return ToFemalePatronymicEN(firstName);
    }

    return firstName;
  }

  private static string ToFemaleLastName(Language language, string familyName)
  {
    if (language == Language.RU)
    {
      return ToFemaleLastNameRU(familyName);
    }
    if (language == Language.EN)
    {
      return ToFemaleLastNameEN(familyName);
    }

    return familyName;
  }

  private static string ToFemalePatronymicRU(string firstName)
  {
    var rule = GetPatronymicRuleRU(firstName);
    var ret = rule switch
    {
      1 => firstName + "овна",
      2 => firstName + "евна",
      31 => firstName[..^1] + "овна",
      32 => firstName[..^1] + "ична",
      4 => firstName + "вна",
      5 => firstName[..^1] + "евна",
      6 => firstName[..^1] + "евна",
      7 => firstName + "вна",
      8 => firstName + "евна",
      91 => firstName[..^2] + "ьевна",
      92 => firstName[..^1] + "евна",
      10 => firstName[..^1] + "евна",
      11 => firstName + "евна",
      12 => firstName[..^1] + "евна",
      13 => firstName + "евна",
      _ => firstName,
    };

    return ret;
  }

  private static string ToFemalePatronymicEN(string firstName)
  {
    return firstName;
  }

  private static string ToFemaleLastNameRU(string familyName)
  {
    var rule = GetLastNameRuleRU(familyName);
    var ret = rule switch
    {
      1 => familyName[..^3] + "ова",
      2 => familyName[..^3] + "ева",
      3 => familyName[..^3] + "ина",
      4 => familyName[..^3] + "ына",
      5 => familyName[..^4] + "ская",
      6 => familyName[..^4] + "цкая",
      7 => familyName[..^2] + "ая",
      8 => familyName[..^2] + "ая",
      9 => familyName[..^1],
      _ => familyName
    };

    return ret;
  }

  private static string ToFemaleLastNameEN(string familyName)
  {
    var rule = GetLastNameRuleEN(familyName);
    var ret = rule switch
    {
      1 => familyName[..^2],
      2 => familyName[..^1],
      _ => familyName
    };

    return ret;
  }
}
