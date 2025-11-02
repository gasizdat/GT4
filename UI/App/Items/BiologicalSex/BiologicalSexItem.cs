using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class BiologicalSexItem : CollectionItemBase<BiologicalSex>
{
  private readonly IBiologicalSexFormatter _biologicalSexFormatter;

  public BiologicalSexItem(BiologicalSex biologicalSex, ServiceProvider services)
    : base(biologicalSex, biologicalSex == BiologicalSex.Male ? "male_stub.png" : "female_stub.png")
  {
    _biologicalSexFormatter = services.GetRequiredService<IBiologicalSexFormatter>();
  }

  public string Name => _biologicalSexFormatter.ToString(Info);
}

