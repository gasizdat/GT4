using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Security.Cryptography;
using System.Text;

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
  // Generates and caches thumbnails for the photos not already cached, loading their blobs in one batch.
  Task PrewarmAsync(IEnumerable<Data?> photos, CancellationToken token);
}

internal sealed class ThumbnailCache : IThumbnailCache
{
  // A node or card only ever shows a small circle; one size serves every consumer (the largest, the
  // family-tree node, is 200px).
  private const float ThumbnailSize = 200;

  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;

  public ThumbnailCache(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
  }

  public ImageSource? Resolve(Data? mainPhoto)
  {
    if (mainPhoto is null || !_CurrentProjectProvider.HasCurrentProject)
      return null;

    var path = PathFor(mainPhoto.Id);
    if (!File.Exists(path))
    {
      var content = mainPhoto.Content.Length > 0 ? mainPhoto.Content : LoadContent(mainPhoto.Id);
      if (!TryWrite(path, content))
        return null;
    }

    return new FileImageSource { File = path };
  }

  public async Task PrewarmAsync(IEnumerable<Data?> photos, CancellationToken token)
  {
    if (!_CurrentProjectProvider.HasCurrentProject)
      return;

    var missing = photos
      .Where(photo => photo is not null)
      .Select(photo => photo!.Id)
      .Distinct()
      .Where(id => !File.Exists(PathFor(id)))
      .ToArray();
    if (missing.Length == 0)
      return;

    var blobs = await _CurrentProjectProvider.Project.Data.GetDataByIdsAsync(missing, token);
    foreach (var data in blobs.Values)
    {
      TryWrite(PathFor(data.Id), data.Content);
    }
  }

  private byte[] LoadContent(int id)
  {
    try
    {
      using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
      var blobs = _CurrentProjectProvider.Project.Data.GetDataByIdsAsync([id], token).Result;
      return blobs.TryGetValue(id, out var data) ? data.Content : [];
    }
    catch
    {
      // The project may have closed underneath us (e.g. backgrounding); fall back to the default image.
      return [];
    }
  }

  private static bool TryWrite(string path, byte[] content)
  {
    if (content.Length == 0)
      return false;
    if (File.Exists(path))
      return true;

    byte[] thumbnail;
    try
    {
      thumbnail = ImageUtils.DownsizedPng(content, ThumbnailSize);
    }
    catch
    {
      // An image that cannot be decoded is left uncached so the caller falls back to the default.
      return false;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    // Write to a temp file and rename into place so two threads racing the same thumbnail can never
    // expose a half-written file; the loser of the rename just discards its copy.
    var temp = $"{path}.{Guid.NewGuid():N}.tmp";
    File.WriteAllBytes(temp, thumbnail);
    try
    {
      File.Move(temp, path);
    }
    catch (IOException)
    {
      // Another thread wrote the same id first; its file is identical (same id => same bytes).
      File.Delete(temp);
    }

    return true;
  }

  private string PathFor(int id) =>
    Path.Combine(FileSystem.CacheDirectory, "thumbs", Scope(), $"{id}.png");

  // A stable per-project namespace so two projects' Data ids (unique only within a project) cannot collide.
  private string Scope()
  {
    var origin = _CurrentProjectProvider.Info.Origin;
    var directory = origin.Directory;
    var key = string.Join("/", [((int)directory.Root).ToString(), .. directory.Path, origin.FileName]);
    var hash = SHA1.HashData(Encoding.UTF8.GetBytes(key));

    return Convert.ToHexString(hash);
  }
}
