using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters;

public interface IRelationshipTypeFormatter
{
  string ToString(
    RelationshipType type, 
    BiologicalSex? biologicalSex, 
    Generation? generation, 
    Consanguinity? consanguinity);
}