using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters.Detailed;

/// <summary>
/// German and English compose kinship over the same generation grid, so only wording and casing
/// differ. That is the sole basis for deriving from <see cref="RelationshipTypeFormatterEn"/>: the
/// grid is built from non-virtual method groups, so a language needing a different table must
/// derive from <see cref="RelationshipTypeFormatterBase"/> as Ru does.
/// </summary>
internal class RelationshipTypeFormatterDe : RelationshipTypeFormatterEn
{
  public RelationshipTypeFormatterDe(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
    : base(type, biologicalSex, generation, consanguinity)
  {
  }

  /// <summary>
  /// German capitalizes every noun, so a capital opening a new word has to survive ("Cousine
  /// zweiten Grades"), while the prefix templates concatenate into one word whose interior capital
  /// must not ("Ur" + "Großvater" -> "Urgroßvater").
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
