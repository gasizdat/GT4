namespace GT4.UI.Abstraction;

public record ModelDescriptor(string FileName, Uri Url, string Sha256)
{
  // Hash-named because a cached file is never re-verified. 16 digits only tell versions apart (the
  // download checks the full hash) and keep the packaged build's redirected AppData path short.
  public string CacheFileName => $"{Sha256[..16]}-{FileName}";
}

public interface IModelDownloader
{
  Task EnsureModelAsync(ModelDescriptor model, CancellationToken token);
  void RemoveUnusedModels(IEnumerable<ModelDescriptor> models);
}
