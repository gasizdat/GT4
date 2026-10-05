using GT4.Core.Project.Dto;

namespace GT4.Core.Gedcom;

/// <summary>
/// The plural a Russian family is named by in GT4 ("Ивановы") from a member's gendered surname: the
/// inverse of the UI's NameDeclension rules.
/// </summary>
internal static class GedcomFamilyName
{
  private const string VelarsAndSibilants = "гкхжшчщ";

  // A null plural is adjectival: it depends on the letter before the ending.
  private static readonly (string Ending, NameType Declension, string? Plural)[] Rules =
  [
    ("ов", NameType.MaleDeclension, "овы"),
    ("ев", NameType.MaleDeclension, "евы"),
    ("ёв", NameType.MaleDeclension, "ёвы"),
    ("ин", NameType.MaleDeclension, "ины"),
    ("ын", NameType.MaleDeclension, "ыны"),
    ("ий", NameType.MaleDeclension, "ие"),
    ("ый", NameType.MaleDeclension, "ые"),
    ("ой", NameType.MaleDeclension, null),
    ("ова", NameType.FemaleDeclension, "овы"),
    ("ева", NameType.FemaleDeclension, "евы"),
    ("ёва", NameType.FemaleDeclension, "ёвы"),
    ("ина", NameType.FemaleDeclension, "ины"),
    ("ына", NameType.FemaleDeclension, "ыны"),
    ("ая", NameType.FemaleDeclension, null),
    ("яя", NameType.FemaleDeclension, "ие"),
  ];

  /// <summary>
  /// The family plural of a Cyrillic surname whose ending is the <paramref name="declension"/>'s form,
  /// else null.
  /// </summary>
  public static string? Plural(string surname, NameType declension)
  {
    if (!surname.All(IsCyrillicLetter))
      return null;

    foreach (var (ending, ruleDeclension, plural) in Rules)
    {
      if (ruleDeclension != declension || surname.Length <= ending.Length || !surname.EndsWith(ending, StringComparison.Ordinal))
        continue;

      var stem = surname[..^ending.Length];
      return stem + (plural ?? AdjectivalPlural(stem[^1]));
    }
    return null;
  }

  private static string AdjectivalPlural(char last) => VelarsAndSibilants.Contains(last) ? "ие" : "ые";

  private static bool IsCyrillicLetter(char c) => c is >= 'Ѐ' and <= 'ӿ' && char.IsLetter(c);
}
