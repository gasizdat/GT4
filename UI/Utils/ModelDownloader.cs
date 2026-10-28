using System.Security.Cryptography;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils;

internal class ModelDownloader(IHttpClientFactory httpClientFactory, IFileSystem fileSystem, IStorage storage) : IModelDownloader
{
  public async Task EnsureModelAsync(ModelDescriptor model, CancellationToken token)
  {
    var target = new FileDescription(storage.Models, model.CacheFileName, null);
    if (fileSystem.FileExists(target))
    {
      // Reaches this path only after the hash check below passes, via an atomic Move -- never re-verified.
      return;
    }

    var temp = new FileDescription(storage.Models, TempFileName(model), null);
    try
    {
      using (var http = httpClientFactory.CreateClient())
      using (var source = await http.GetStreamAsync(model.Url, token).ConfigureAwait(false))
      {
        fileSystem.Copy(source, temp);
      }

      string hash;
      using (var hashStream = fileSystem.OpenReadStream(temp))
      {
        hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, token).ConfigureAwait(false));
      }
      if (!hash.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase))
      {
        throw new ModelDownloadException(
          $"Downloaded '{model.FileName}' failed integrity verification.",
          new InvalidDataException($"Expected SHA-256 {model.Sha256}, got {hash}."));
      }

      fileSystem.Move(temp, target);
    }
    catch (Exception ex) when (ex is not OperationCanceledException and not ModelDownloadException)
    {
      throw new ModelDownloadException($"Failed to download the '{model.FileName}' model.", ex);
    }
    finally
    {
      if (fileSystem.FileExists(temp))
      {
        fileSystem.RemoveFile(temp);
      }
    }
  }

  // The app isn't single-instance, so a listed model's .download may be another instance's in-flight write.
  public void RemoveUnusedModels(IEnumerable<ModelDescriptor> models)
  {
    var used = models
      .SelectMany(model => new[] { model.CacheFileName, TempFileName(model) })
      .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var unused = fileSystem
      .GetFiles(storage.Models, "*", recursive: false)
      .Where(file => !used.Contains(file.FileName));

    foreach (var file in unused)
    {
      fileSystem.RemoveFile(file);
    }
  }

  private static string TempFileName(ModelDescriptor model) => $"{model.CacheFileName}.download";
}
