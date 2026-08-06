namespace GT4.Core.Project.Extensions;

using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

public static class PersonDataExtensions
{
  /// <summary>
  /// Fetches a photo slot (main or additional), merging plain and tagged categories into one
  /// per-person array. Concatenating rather than overwriting is safe for both callers: a main
  /// photo is only ever one or the other, and additional photos can freely mix both.
  /// </summary>
  public static Task<Dictionary<int, Data[]>> GetMergedPhotoSetAsync(
    this ITablePersonData personData, Person[] persons, DataCategory plainCategory, CancellationToken token) =>
    MergePhotoSetAsync(plainCategory, (category, t) => personData.GetPersonDataSetAsync(persons, category, t), token);

  /// <summary>
  /// Same merge as <see cref="GetMergedPhotoSetAsync"/>, but each <see cref="Data"/>'s Content is left
  /// empty -- for callers that only need to know a photo exists (and its Id), not decode it.
  /// </summary>
  public static Task<Dictionary<int, Data[]>> GetMergedPhotoMetadataSetAsync(
    this ITablePersonData personData, Person[] persons, DataCategory plainCategory, CancellationToken token) =>
    MergePhotoSetAsync(plainCategory, (category, t) => personData.GetPersonDataMetadataSetAsync(persons, category, t), token);

  private static async Task<Dictionary<int, Data[]>> MergePhotoSetAsync(
    DataCategory plainCategory, Func<DataCategory, CancellationToken, Task<Dictionary<int, Data[]>>> fetch, CancellationToken token)
  {
    var tagged = plainCategory.AsTaggedPhoto();
    var plainTask = fetch(plainCategory, token);
    var taggedTask = fetch(tagged, token);
    await Task.WhenAll(plainTask, taggedTask);

    var merged = new Dictionary<int, Data[]>(plainTask.Result);
    foreach (var (personId, photos) in taggedTask.Result)
    {
      merged[personId] = merged.TryGetValue(personId, out var existing) ? [.. existing, .. photos] : photos;
    }
    return merged;
  }
}
