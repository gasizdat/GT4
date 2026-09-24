namespace GT4.Core.Project.Extensions;

using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

public static class NameDataExtensions
{
  /// <summary>
  /// Fetches a family photo slot (main or additional), merging plain and tagged categories into one
  /// per-name array. See <see cref="PersonDataExtensions.GetMergedPhotoSetAsync"/> for why concatenating
  /// rather than overwriting is safe.
  /// </summary>
  public static async Task<Dictionary<int, Data[]>> GetMergedPhotoSetAsync(
    this ITableNameData nameData, Name[] names, DataCategory plainCategory, CancellationToken token)
  {
    var tagged = plainCategory.AsTaggedPhoto();
    var plainTask = nameData.GetNameDataSetAsync(names, plainCategory, token);
    var taggedTask = nameData.GetNameDataSetAsync(names, tagged, token);
    await Task.WhenAll(plainTask, taggedTask);

    var merged = new Dictionary<int, Data[]>(plainTask.Result);
    foreach (var (nameId, photos) in taggedTask.Result)
    {
      merged[nameId] = merged.TryGetValue(nameId, out var existing) ? [.. existing, .. photos] : photos;
    }
    return merged;
  }
}
