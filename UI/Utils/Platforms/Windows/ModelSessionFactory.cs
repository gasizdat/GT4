#if WINDOWS
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Platforms.Windows;

public interface IModelSessionFactory
{
  Task<InferenceSession> LoadSessionAsync(ModelDescriptor model, CancellationToken token);
  void Release(ModelDescriptor[] models);
}

internal class ModelSessionFactory(IModelDownloader downloader, IFileSystem fileSystem, IStorage storage) : IModelSessionFactory
{
  private readonly Dictionary<ModelDescriptor, InferenceSession> _Sessions = new();
  private readonly SemaphoreSlim _Gate = new(1, 1);

  public async Task<InferenceSession> LoadSessionAsync(ModelDescriptor model, CancellationToken token)
  {
    await _Gate.WaitAsync(token);
    try
    {
      if (!_Sessions.TryGetValue(model, out var session))
      {
        await downloader.EnsureModelAsync(model, token);
        var path = fileSystem.ToPath(new FileDescription(storage.Models, model.CacheFileName, null));
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
