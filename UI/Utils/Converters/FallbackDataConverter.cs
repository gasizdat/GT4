using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Converters;

internal class FallbackDataConverter : IDataConverter
{
  public Task<Data?> FromObjectAsync(object? data, CancellationToken token) => throw new NotSupportedException();
  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    System.Diagnostics.Debug.WriteLine($"No usable IDataConverter result for {data?.Category}, ID: {data?.Id}; applying fallback.");
    return null;
  }
}
