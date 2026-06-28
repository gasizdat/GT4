using GT4.Core.Project.Dto;

namespace GT4.UI.Utils;

/// <summary>
/// On-disk cache of downsized person main-photo thumbnails. Keyed by the immutable <c>Data.Id</c>: a
/// changed photo always gets a new id, so a cached thumbnail can never go stale. Lists pre-warm the cache
/// in one batched blob read (<see cref="PrewarmAsync"/>); any lookup that was not pre-warmed self-heals by
/// loading its single blob on demand, so <see cref="Resolve"/> is always correct, never merely faster.
/// </summary>
public interface IThumbnailCache
{
  // A thumbnail ImageSource for the photo, or null when there is no resolvable photo (caller shows a default).
  ImageSource? Resolve(Data? mainPhoto);
  // Generates and caches thumbnails for the photos not already cached, using each photo's content when it
  // already carries it and batch-loading the blobs only for the content-less (id-only) references.
  Task PrewarmAsync(IEnumerable<Data?> photos, CancellationToken token);
}
