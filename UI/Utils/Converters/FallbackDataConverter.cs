using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Converters;

internal class FallbackDataConverter : IDataConverter
{
  private readonly DataCategory _Category;

  public FallbackDataConverter(DataCategory category) => _Category = category;

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token) =>
    throw new NotSupportedException($"No IDataConverter registered for {_Category}.");

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    System.Diagnostics.Debug.WriteLine($"No IDataConverter registered for {_Category}; Data ID: {data?.Id}.");
    return null;
  }
}
