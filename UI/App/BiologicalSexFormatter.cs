using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class BiologicalSexFormatter : IBiologicalSexFormatter
{
  public string ToString(BiologicalSex? biologicalSex)
  {
    switch (biologicalSex)
    {
      case BiologicalSex.Male:
        return UIStrings.BiologicalSexMale;
        case BiologicalSex.Female:
        return UIStrings.BiologicalSexFemale;
      default:
        return UIStrings.BiologicalSexUnknown;
    }
  }
}