using System.Net;
using System.Reflection;
using GT4.Core.Project.Tests;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Platforms.Windows;
using Moq;

namespace GT4.UI.Utils.Tests;

internal sealed class FixtureModelCache : IDisposable
{
  private readonly string _Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
  private readonly FixedStorage _Storage = new(new DirectoryDescription(Environment.SpecialFolder.ApplicationData, ["models"]));
  private readonly DiskFileSystem _FileSystem;

  public FixtureModelCache()
  {
    _FileSystem = new DiskFileSystem(_Root);
  }

  public void Dispose()
  {
    if (Directory.Exists(_Root))
    {
      Directory.Delete(_Root, recursive: true);
    }
  }

  public void Seed(ModelDescriptor model, string fixtureFileName)
  {
    var assembly = Assembly.GetExecutingAssembly();
    var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".{fixtureFileName}", StringComparison.Ordinal));
    using var fixture = assembly.GetManifestResourceStream(name)!;
    _FileSystem.Copy(fixture, new FileDescription(_Storage.Models, model.CacheFileName, null));
  }

  public IModelSessionProvider SessionProvider()
  {
    var factory = new Mock<IHttpClientFactory>();
    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new FailingHandler()));
    var downloader = new ModelDownloader(factory.Object, _FileSystem, _Storage);
    return new ModelSessionProvider(downloader, _FileSystem, _Storage);
  }

  private sealed class FailingHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
  }
}

internal sealed class FixedStorage(DirectoryDescription models) : IStorage
{
  public DirectoryDescription ProjectsCache => throw new NotSupportedException();
  public DirectoryDescription ProjectsRoot => throw new NotSupportedException();
  public DirectoryDescription AppConfig => throw new NotSupportedException();
  public DirectoryDescription Models { get; } = models;
}
