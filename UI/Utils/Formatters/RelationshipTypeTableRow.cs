using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters.Detailed;

internal record class RelationshipTypeTableRow(string F, string M, string U, RelationshipType? SubType = null)
{

  public RelationshipTypeTableRow(RelationshipType subType)
    : this(F: string.Empty, M: string.Empty, U: string.Empty, SubType: subType)
  {

  }

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
