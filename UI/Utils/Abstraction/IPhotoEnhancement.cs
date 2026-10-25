using GT4.Core.Project.Dto;

namespace GT4.UI.Abstraction;

public interface IPhotoEnhancement
{
  string DisplayName { get; }
  Task<Data> ApplyAsync(Data photo, CancellationToken token);
}
