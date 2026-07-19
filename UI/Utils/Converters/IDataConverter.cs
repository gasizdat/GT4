using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Converters;

public interface IDataConverter
{
  Task<Data?> FromObjectAsync(object? data, CancellationToken token);
  Task<object?> ToObjectAsync(Data? data, CancellationToken token);
}

public delegate IDataConverter DataConverterResolver(DataCategory category);

/// <summary>Like <see cref="DataConverterResolver"/>, but tolerates a category with no registered
/// converter (returns null) instead of throwing -- for callers resolving an arbitrary runtime
/// category, not one they registered themselves.</summary>
public delegate IDataConverter? OptionalDataConverterResolver(DataCategory category);