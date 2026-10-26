using System.Security.Cryptography;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils;

internal class ModelDownloader(IHttpClientFactory httpClientFactory, IFileSystem fileSystem, IStorage storage) : IModelDownloader
{
  public async Task EnsureModelAsync(ModelDescriptor model, CancellationToken token)
  {
    var target = new FileDescription(storage.Models, model.FileName, null);
    if (fileSystem.FileExists(target))
    {
      // Reaches this path only after the hash check below passes, via an atomic Move -- never re-verified.
      return;
    }

    var temp = new FileDescription(storage.Models, $"{model.FileName}.download", null);
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
}
