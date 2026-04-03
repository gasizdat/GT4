namespace GT4.UI.Utils;

partial class NameDeclension
{
  // See RussianPatronymicRules.md for details
  private static int GetPatronymicRuleRU(string name)
  {
    const string consonants1 = "бвгдзйклмнпрстфх";
    const string consonants2 = "жшчщц";
    const string vowels1 = "ауы";

    bool IsConsonant(char value) => consonants1.Contains(value) || consonants2.Contains(value);
    bool IsVowel(char value) => !IsConsonant(value) && !"ьъ".Contains(value);

    var w = name.Length > 3 ? name[..^3].Last() : '\0';
    var x = name.Length > 2 ? name[..^2].Last() : '\0';
    var y = name.Length > 1 ? name[..^1].Last() : '\0';
    var z = name.LastOrDefault('\0');

    if (y == z && IsVowel(z) || ("ае".Contains(y) && z == 'у'))
    {
      return 13;
    }
    if (y == 'и' && z == 'й')
    {
      if ("кхц".Contains(x))
      {
        return 92;
      }
      if (IsVowel(w) && IsConsonant(x))
      {
        return 91;
      }
      if (w == 'н' && x == 'т')
      {
        return 91;
      }

      return 92;
    }
    if (IsVowel(y) && z == 'й')
    {
      return 12;
    }
    if (consonants1.Contains(z))
    {
      return 1;
    }
    if (consonants2.Contains(z))
    {
      return 2;
    }
    if (consonants2.Contains(y) && IsVowel(z))
    {
      return 5;
    }
    if (vowels1.Contains(z))
    {
      var isException = name.ToLower() switch
      {
        "аникита" or
        "никита" or
        "мина" or
        "савва" or
        "сила" or
        "фока" => true,
        _ => false
      };
      if (isException)
      {
        return 32;
      }
      return 31;
    }
    if (z == 'о')
    {
      return 4;
    }
    if (IsConsonant(y) && z == 'ь')
    {
      return 6;
    }
    if (z == 'е')
    {
      return 7;
    }
    if (z == 'и')
    {
      return 8;
    }
    if ("еи".Contains(y) && z == 'я')
    {
      return 10;
    }
    if (IsVowel(z))
    {
      return 11;
    }

    return 0;
  }
}
