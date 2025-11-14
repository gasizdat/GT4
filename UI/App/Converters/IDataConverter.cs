using GT4.Core.Project.Dto;

namespace GT4.UI.Converters;

public interface IDataConverter
{
  Task<Data?> FromObjectAsync(object? data, CancellationToken token);
  Task<object?> ToObjectAsync(Data? data, CancellationToken token);
}