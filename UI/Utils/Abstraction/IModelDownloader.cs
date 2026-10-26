namespace GT4.UI.Abstraction;

public record ModelDescriptor(string FileName, Uri Url, string Sha256);

public interface IModelDownloader
{
  Task EnsureModelAsync(ModelDescriptor model, CancellationToken token);
}
