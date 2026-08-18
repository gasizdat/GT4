using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters.Detailed;

namespace GT4.UI.Utils.Formatters;

internal class RelationshipTypeFormatter : IRelationshipTypeFormatter
{
  public string ToString(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (generation.HasValue != consanguinity.HasValue)
    {
      throw new ArgumentException("generation.HasValue != consanguinity.HasValue");
    }
    if (consanguinity.HasValue && consanguinity < Consanguinity.Zero)
    {
      throw new ArgumentException("consanguinity < Consanguinity.Zero");
    }

    var ret = Format(type, biologicalSex ?? BiologicalSex.Unknown, generation, consanguinity);

#if DEBUG
    if (!RelationshipTypeFormatterBase.IsRunningInTest)
    {
      var gen = generation ?? Generation.Zero;
      var con = consanguinity ?? Consanguinity.Zero;
      ret = $"{ret} G{gen.Value} C{con.Value}";
    }
#endif

    return ret;
  }

  /// <summary>
  /// Languages differ over which relationships they have an unknown-sex word for: German says
  /// "Großelternteil" but has to spell out "Großonkel oder Großtante". Where the word is missing,
  /// building the label from two labels formatted in full is what puts a generation prefix and a
  /// consanguinity adjective on both halves, each agreeing with the half it sits on.
  /// </summary>
  private static string Format(RelationshipType type, BiologicalSex biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    var formatter = Create(type, biologicalSex, generation, consanguinity);
    var ret = formatter.ToString();

    if (!formatter.NeutralTermMissing)
    {
      return ret;
    }

    var male = Create(type, BiologicalSex.Male, generation, consanguinity);
    var female = Create(type, BiologicalSex.Female, generation, consanguinity);

    return formatter.Join(male.ToString(), female.ToString());
  }

  private static RelationshipTypeFormatterBase Create(RelationshipType type, BiologicalSex biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    if (Language.Current == Language.RU)
    {
      return new RelationshipTypeFormatterRu(type, biologicalSex, generation, consanguinity);
    }
    else if (Language.Current == Language.DE)
    {
      return new RelationshipTypeFormatterDe(type, biologicalSex, generation, consanguinity);
    }
    else if (Language.Current == Language.ES)
    {
      return new RelationshipTypeFormatterEs(type, biologicalSex, generation, consanguinity);
    }
    else
    {
      return new RelationshipTypeFormatterEn(type, biologicalSex, generation, consanguinity);
    }
  }
}
