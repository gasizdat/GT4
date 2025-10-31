using GT4.Core.Project.Dto;

namespace GT4.UI;

public interface IRelationshipTypeFormatter
{
  string GetRelationshipTypeName(RelationshipType type);
}