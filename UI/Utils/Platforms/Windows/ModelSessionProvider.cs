#if WINDOWS
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Platforms.Windows;

internal class ModelSessionProvider(IModelDownloader downloader, IFileSystem fileSystem, IStorage storage) : IModelSessionProvider
{
  private readonly Dictionary<ModelDescriptor, InferenceSession> _Sessions = new();
  private readonly SemaphoreSlim _Gate = new(1, 1);

  public async Task<InferenceSession> GetSessionAsync(ModelDescriptor model, CancellationToken token)
  {
    await _Gate.WaitAsync(token);
    try
    {
      if (!_Sessions.TryGetValue(model, out var session))
      {
        await downloader.EnsureModelAsync(model, token);
        var file = new FileDescription(storage.Models, model.CacheFileName, null);
        var path = fileSystem.ToPath(file);
        session = new InferenceSession(path);
        _Sessions.Add(model, session);
      }
      return session;
    }
    finally
    {
      _Gate.Release();
    }
  }

  public void Release(ModelDescriptor[] models)
  {
    _Gate.Wait();
    try
    {
      foreach (var model in models)
      {
        if (_Sessions.Remove(model, out var session))
        {
          session.Dispose();
        }
      }
    }
    finally
    {
      _Gate.Release();
    }
  }
}
#endif
