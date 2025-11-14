using GT4.Core.Project.Dto;

namespace GT4.UI.Formatters;

public interface IBiologicalSexFormatter
{
  string ToString(BiologicalSex? biologicalSex);
}