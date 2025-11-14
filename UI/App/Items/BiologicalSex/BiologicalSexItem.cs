using GT4.Core.Project.Dto;

namespace GT4.UI.Items;

public class BiologicalSexItem : CollectionItemBase<BiologicalSex>
{
  private readonly IBiologicalSexFormatter _BiologicalSexFormatter;

  public BiologicalSexItem(BiologicalSex biologicalSex, IBiologicalSexFormatter biologicalSexFormatter)
    : base(biologicalSex, biologicalSex == BiologicalSex.Male ? "male_stub.png" : "female_stub.png")
  {
    _BiologicalSexFormatter = biologicalSexFormatter;
  }

  public string Name => _BiologicalSexFormatter.ToString(Info);
}

