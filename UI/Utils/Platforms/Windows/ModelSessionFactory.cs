#if WINDOWS
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Platforms.Windows;

internal static class ModelSessionFactory
{
  public static async Task<InferenceSession> LoadSessionAsync(
    IModelDownloader downloader, IFileSystem fileSystem, IStorage storage, ModelDescriptor model, CancellationToken token)
  {
    await downloader.EnsureModelAsync(model, token);

    using var stream = fileSystem.OpenReadStream(new FileDescription(storage.Models, model.FileName, null));
    using var bytes = new MemoryStream();
    stream.CopyTo(bytes);
    return new InferenceSession(bytes.ToArray());
  }
}
#endif
