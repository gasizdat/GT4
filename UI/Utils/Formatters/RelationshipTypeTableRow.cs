using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters.Detailed;

internal record class RelationshipTypeTableRow(string F, string M, string U, RelationshipType? SubType = null)
{
  public string ToString(BiologicalSex biologicalSex)
  {
    var ret = biologicalSex switch
    {
      BiologicalSex.Female => F,
      BiologicalSex.Male => M,
      _ => U
    };

    return ret;
  }
}
