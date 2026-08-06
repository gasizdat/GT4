using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Utils;

public sealed class PhotoCache
{
  private const int MaxEntries = 64;

  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly Dictionary<int, PhotoInfo> _Cache = new();
  private readonly object _Sync = new();
  private ProjectInfo? _LastProjectInfo;

  public PhotoCache(ICurrentProjectProvider currentProjectProvider)
  {
    _CurrentProjectProvider = currentProjectProvider;
  }

  public async Task<PhotoInfo> GetOrResolveAsync(
    OptionalDataConverterResolver dataConverterResolver, Data data, ImageSource fallback, float maxSize)
  {
    lock (_Sync)
    {
      InvalidateIfProjectChanged();
      if (_Cache.TryGetValue(data.Id, out var cached))
      {
        return cached;
      }
    }

    // CancellationToken.None, not a caller-supplied token: this resolution is shared by every
    // concurrent caller asking for the same Data.Id, and one caller's short-lived token being
    // cancelled/disposed mid-resolve must not poison the entry the others are waiting on.
    var resolved = await ImageUtils.ResolvePhotoAsync(dataConverterResolver, data, fallback, CancellationToken.None);
    if (ReferenceEquals(resolved.Source, fallback))
    {
      // ResolvePhotoAsync couldn't resolve data.Category at all -- this is the caller's own
      // placeholder, not this photo, so there is nothing to cache under data.Id.
      return resolved;
    }

    var downsized = await ImageUtils.DownsizedAsync(resolved, maxSize, CancellationToken.None);
    if (ReferenceEquals(downsized, resolved))
    {
      // DownsizedAsync fell back to the undecoded original -- caching it would occupy one of the 64
      // slots with the full-resolution bitmap this cache exists to avoid pinning.
      return downsized;
    }

    lock (_Sync)
    {
      if (_Cache.Count >= MaxEntries)
      {
        _Cache.Clear();
      }
      _Cache[data.Id] = downsized;
    }

    return downsized;
  }

  // Data.Id is stable within one open project (an edit always inserts a new Data row rather than
  // updating one in place), but a project close/reopen or revision restore replaces the whole SQLite
  // file and rewinds Data's AUTOINCREMENT sequence, so a stale entry could point at a different photo.
  private void InvalidateIfProjectChanged()
  {
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      _Cache.Clear();
      _LastProjectInfo = _CurrentProjectProvider.Info;
    }
  }
}
