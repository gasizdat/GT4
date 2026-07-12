namespace GT4.Core.Project.Extensions;

using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

public static class PersonDataExtensions
{
  /// <summary>
  /// Fetches a photo slot (main or additional) for a set of persons, merging the plain and tagged
  /// categories into one per-person array. The tagged category is derived from <paramref name="plainCategory"/>
  /// via <see cref="DataCategoryExtensions.AsTaggedPhoto"/> rather than taken as a second parameter, so a
  /// non-photo category (e.g. <see cref="DataCategory.PersonBio"/>) throws instead of silently merging
  /// unrelated data, and the two categories can never be a mismatched pair. Concatenating (rather than
  /// overwriting) is correct for both callers: a main photo is either plain or tagged, never both, so
  /// concatenation degrades to the same result as an overwrite; additional photos can freely mix plain and
  /// tagged rows for the same person.
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
