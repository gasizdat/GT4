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
  public static async Task<Dictionary<int, Data[]>> GetMergedPhotoSetAsync(
    this ITablePersonData personData, Person[] persons, DataCategory plainCategory, CancellationToken token)
  {
    var tagged = plainCategory.AsTaggedPhoto();
    var plainTask = personData.GetPersonDataSetAsync(persons, plainCategory, token);
    var taggedTask = personData.GetPersonDataSetAsync(persons, tagged, token);
    await Task.WhenAll(plainTask, taggedTask);

    var merged = new Dictionary<int, Data[]>(plainTask.Result);
    foreach (var (personId, photos) in taggedTask.Result)
    {
      merged[personId] = merged.TryGetValue(personId, out var existing) ? [.. existing, .. photos] : photos;
    }
    return merged;
  }
}
