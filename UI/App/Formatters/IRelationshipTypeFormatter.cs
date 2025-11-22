using GT4.Core.Project.Dto;

namespace GT4.UI.Formatters;

public interface IRelationshipTypeFormatter
{
  string ToString(RelationshipType type, BiologicalSex? biologicalSex);
}