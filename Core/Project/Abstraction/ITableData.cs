using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ITableData
{
  Task<Data> AddDataAsync(byte[] content, string? mimeType, DataCategory dataCategory, CancellationToken token);
  Task RemoveDataAsync(Data data, CancellationToken token);
  Task<Data?> TryGetDataByIdAsync(int? id, CancellationToken token);
}