#if WINDOWS
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Platforms.Windows;

public interface IModelSessionFactory
{
  Task<InferenceSession> LoadSessionAsync(ModelDescriptor model, CancellationToken token);
}

internal class ModelSessionFactory(IModelDownloader downloader, IFileSystem fileSystem, IStorage storage) : IModelSessionFactory
{
  public async Task<InferenceSession> LoadSessionAsync(ModelDescriptor model, CancellationToken token)
  {
    await downloader.EnsureModelAsync(model, token);

    return new InferenceSession(fileSystem.ToPath(new FileDescription(storage.Models, model.CacheFileName, null)));
  }
}
#endif
