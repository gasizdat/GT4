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

    return ToMalePatronymicEN(firstName);
  }

  private static string ToMaleLastName(Language language, string firstName)
  {
    if (language == Language.RU)
    {
      return ToMaleLastNameRU(firstName);
    }

    return ToMaleLastNameEN(firstName);
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
    var single = ToSingleRU(familyName);
    var ret = single;

    return ret;
  }

  private static string ToMaleLastNameEN(string familyName)
  {
    var single = ToSingleEN(familyName);
    return single;
  }
}
