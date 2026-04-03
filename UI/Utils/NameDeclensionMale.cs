using GT4.Core.Project.Dto;

namespace GT4.UI.Utils;

partial class NameDeclension
{
  public static string ToMaleDeclension(Language language, NameType nameType, string name)
  {
    var noDeclensionNameType = nameType & NameType.NoDeclension;
    var ret = noDeclensionNameType switch
    {
      NameType.FirstName => ToMalePatronymic(language, name),
      NameType.FamilyName => ToMaleLastName(language, name),
      _ => name
    };
    return ret;
  }

  private static string ToMalePatronymic(Language language, string firstName)
  {
    if (language == Language.RU)
    {
      return ToMalePatronymicRU(firstName);
    }
    if (language == Language.EN)
    {
      return ToMalePatronymicEN(firstName);
    }

    return firstName;
  }

  private static string ToMaleLastName(Language language, string familyName)
  {
    if (language == Language.RU)
    {
      return ToMaleLastNameRU(familyName);
    }
    if (language == Language.EN)
    {
      return ToMaleLastNameEN(familyName);
    }

    return familyName;
  }

  private static string ToMalePatronymicRU(string firstName)
  {
    var rule = GetPatronymicRuleRU(firstName);
    var ret = rule switch
    {
      1 => firstName + "ович",
      2 => firstName + "евич",
      31 => firstName[..^1] + "ович",
      32 => firstName[..^1] + "ич",
      4 => firstName + "вич",
      5 => firstName[..^1] + "евич",
      6 => firstName[..^1] + "евич",
      7 => firstName + "вич",
      8 => firstName + "евич",
      91 => firstName[..^2] + "ьевич",
      92 => firstName[..^1] + "евич",
      10 => firstName[..^1] + "евич",
      11 => firstName + "евич",
      12 => firstName[..^1] + "евич",
      13 => firstName + "евич",
      _ => firstName,
    };

    return ret;
  }

  private static string ToMalePatronymicEN(string firstName)
  {
    return firstName;
  }

  private static string ToMaleLastNameRU(string familyName)
  {
    var rule = GetLastNameRuleRU(familyName);
    var ret = rule switch
    {
      1 => familyName[..^3] + "ов",
      2 => familyName[..^3] + "ев",
      3 => familyName[..^3] + "ин",
      4 => familyName[..^3] + "ын",
      5 => familyName[..^4] + "ский",
      6 => familyName[..^4] + "цкий",
      7 => familyName[..^2] + "ый",
      8 => familyName[..^2] + "ий",
      9 => familyName[..^1],
      _ => familyName
    };

    return ret;
  }

  private static string ToMaleLastNameEN(string familyName)
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
