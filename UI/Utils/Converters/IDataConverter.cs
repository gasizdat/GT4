using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Converters;

public interface IDataConverter
{
  Task<Data?> FromObjectAsync(object? data, CancellationToken token);
  Task<object?> ToObjectAsync(Data? data, CancellationToken token);
}

/// <summary>Null means no converter is registered for the category -- not an error condition by
/// itself; callers that require one throw explicitly.</summary>
public delegate IDataConverter? DataConverterResolver(DataCategory category);
