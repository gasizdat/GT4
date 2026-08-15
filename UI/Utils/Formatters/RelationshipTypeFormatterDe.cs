using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters.Detailed;

/// <summary>
/// German composes kinship exactly as English does -- the same generation grid, the same
/// grand-/great- and "Nth cousin M times removed" constructions -- so only the wording and the
/// casing differ.
/// </summary>
internal class RelationshipTypeFormatterDe : RelationshipTypeFormatterEn
{
  public RelationshipTypeFormatterDe(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
    : base(type, biologicalSex, generation, consanguinity)
  {
  }

  /// <summary>
  /// German capitalizes every noun, so a capital opening a new word has to survive ("Cousine
  /// zweiten Grades"), while the prefix templates concatenate into a single word whose interior
  /// capital must not ("Ur" + "Großvater" -> "Urgroßvater"). Lowercasing only what follows a letter
  /// separates the two cases.
  /// </summary>
  protected override string Normalize(string composed)
  {
    var chars = composed.ToCharArray();

    for (var i = 1; i < chars.Length; i++)
    {
      if (char.IsLetter(chars[i - 1]))
      {
        chars[i] = char.ToLower(chars[i]);
      }
    }

    var ret = new string(chars);

    return ret;
  }
}
