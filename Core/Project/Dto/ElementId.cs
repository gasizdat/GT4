namespace GT4.Core.Project.Dto;

public record class ElementId(int Id)
{
  /// <summary>Sentinel <see cref="Id"/> for a record that hasn't been persisted yet.</summary>
  public const int NonCommittedId = 0;
}
