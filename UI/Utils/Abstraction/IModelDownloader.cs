namespace GT4.UI.Abstraction;

public record ModelDescriptor(string FileName, Uri Url, string Sha256)
{
  // A replaced model's new bytes land under a new cache name instead of reusing the old file, which
  // is trusted without re-hashing once present. Integrity is checked against the full Sha256 at
  // download time; the prefix only needs to tell versions apart while keeping the packaged build's
  // redirected AppData path short.
  public string CacheFileName => $"{Sha256[..16]}-{FileName}";
}

public interface IModelDownloader
{
  Task EnsureModelAsync(ModelDescriptor model, CancellationToken token);
  void RemoveUnusedModels(IEnumerable<ModelDescriptor> models);
}
