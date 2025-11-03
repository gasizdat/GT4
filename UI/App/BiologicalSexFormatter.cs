using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class BiologicalSexFormatter : IBiologicalSexFormatter
{
  public string ToString(BiologicalSex? biologicalSex)
  {
    return biologicalSex switch
    {
      BiologicalSex.Male => UIStrings.BiologicalSexMale,
      BiologicalSex.Female => UIStrings.BiologicalSexFemale,
      _ => UIStrings.BiologicalSexUnknown
    };
  }
}