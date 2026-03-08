using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters.Detailed;

namespace GT4.UI.Utils.Formatters;

public class RelationshipTypeFormatter : IRelationshipTypeFormatter
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

    RelationshipTypeFormatterBase formatter;

    if (Language.Current == Language.RU)
    {
      formatter = new RelationshipTypeFormatterRu(type, biologicalSex, generation, consanguinity);
    }
    else
    {
      formatter = new RelationshipTypeFormatterEn(type, biologicalSex, generation, consanguinity);
    }

    var ret = formatter.ToString();
    return ret;
  }
}
