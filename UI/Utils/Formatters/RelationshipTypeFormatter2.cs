using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters;

public class RelationshipTypeFormatter2 : IRelationshipTypeFormatter
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

    string ret;

    if (Language.Current == Language.RU)
    {
      ret = new Detailed.RelationshipTypeFormatterRu(type, biologicalSex, generation, consanguinity).ToString();
    }
    else
    {
      throw new NotImplementedException($"Formatter for language {Language.Current.Code} is not implemented");
    }

    return ret;
  }
}
