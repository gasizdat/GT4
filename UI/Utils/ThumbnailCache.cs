using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Security.Cryptography;
using System.Text;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils;

internal sealed class ThumbnailCache : IThumbnailCache
{
  // A node or card only ever shows a small circle; one size serves every consumer (the largest, the
  // family-tree node, is 200px).
  private const float ThumbnailSize = 200;

  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IFileSystem _FileSystem;
  private readonly IStorage _Storage;
  private readonly IImageDownsizer _Downsizer;

  public ThumbnailCache(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IFileSystem fileSystem,
    IStorage storage,
    IImageDownsizer downsizer)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _FileSystem = fileSystem;
    _Storage = storage;
    _Downsizer = downsizer;
  }

  public ImageSource? Resolve(Data? mainPhoto)
  {
    if (mainPhoto is null || !_CurrentProjectProvider.HasCurrentProject)
      return null;

    var file = FileFor(mainPhoto.Id);
    if (!_FileSystem.FileExists(file))
    {
      var content = mainPhoto.Content.Length > 0 ? mainPhoto.Content : LoadContent(mainPhoto.Id);
      if (!TryWrite(file, content))
        return null;
    }

    return new FileImageSource { File = _FileSystem.ToPath(file) };
  }

  public async Task PrewarmAsync(IEnumerable<Data?> photos, CancellationToken token)
  {
    if (!_CurrentProjectProvider.HasCurrentProject)
      return;

    var uncached = photos
      .OfType<Data>()
      .DistinctBy(photo => photo.Id)
      .Where(photo => !_FileSystem.FileExists(FileFor(photo.Id)))
      .ToArray();

    // A photo that already carries its content (e.g. loaded via MainPhoto.Load) is written straight away;
    // only the content-less references (loaded id-only) need a single batched blob read.
    var references = new List<int>();
    foreach (var photo in uncached)
    {
      if (photo.Content.Length > 0)
        TryWrite(FileFor(photo.Id), photo.Content);
      else
        references.Add(photo.Id);
    }
    if (references.Count == 0)
      return;

    var blobs = await _CurrentProjectProvider.Project.Data.GetDataByIdsAsync([.. references], token);
    foreach (var data in blobs.Values)
    {
      TryWrite(FileFor(data.Id), data.Content);
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

  private bool TryWrite(FileDescription file, byte[] content)
  {
    if (content.Length == 0)
      return false;
    if (_FileSystem.FileExists(file))
      return true;

    byte[] thumbnail;
    try
    {
      thumbnail = _Downsizer.Downsize(content, ThumbnailSize);
    }
    catch
    {
      // An image that cannot be decoded is left uncached so the caller falls back to the default.
      return false;
    }

    // Write to a temp file and atomically publish it, so a concurrent reader never sees a half-written
    // thumbnail; the loser of a same-id race just discards its copy (the winner's file is identical).
    var temp = file with { FileName = $"{file.FileName}.{Guid.NewGuid():N}.tmp" };
    using (var stream = _FileSystem.OpenWriteStream(temp))
    {
      stream.Write(thumbnail);
    }

    try
    {
      _FileSystem.Move(temp, file);
    }
    catch (IOException)
    {
      _FileSystem.RemoveFile(temp);
    }

    return true;
  }

  private FileDescription FileFor(int id)
  {
    var cache = _Storage.ProjectsCache;
    var directory = cache with { Path = [.. cache.Path, "thumbs", Scope()] };

    return new FileDescription(directory, $"{id}.png", "image/png");
  }

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
