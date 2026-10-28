using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Moq;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ModelDownloaderTests : IDisposable
{
  private const string UnverifiedHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

  private readonly string _Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
  private readonly FixedStorage _Storage;
  private readonly TempFileSystem _FileSystem;

  public ModelDownloaderTests()
  {
    _FileSystem = new TempFileSystem(_Root);
    _Storage = new FixedStorage(new DirectoryDescription(Environment.SpecialFolder.ApplicationData, ["models"]));
  }

  public void Dispose()
  {
    if (Directory.Exists(_Root))
    {
      Directory.Delete(_Root, recursive: true);
    }
  }

  [Fact]
  public async Task AlreadyPresentModel_NoHttpCallMade()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash);
    Seed(model.CacheFileName);
    var handler = new RecordingHandler([1, 2, 3]);

    await Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    handler.RequestCount.Should().Be(0);
  }

  [Fact]
  public async Task SameFileNameWithNewHash_DownloadsInsteadOfReusingTheOldFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var hash = Convert.ToHexString(SHA256.HashData(content));
    var stale = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash);
    var model = stale with { Sha256 = hash };
    Seed(stale.CacheFileName);
    var handler = new RecordingHandler(content);

    await Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    handler.RequestCount.Should().Be(1);
    _FileSystem.FileExists(new FileDescription(_Storage.Models, model.CacheFileName, null)).Should().BeTrue();
  }

  [Fact]
  public async Task FreshDownload_HashMatches_PlacesVerifiedFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var hash = Convert.ToHexString(SHA256.HashData(content));
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), hash);
    var handler = new RecordingHandler(content);

    await Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    var target = new FileDescription(_Storage.Models, model.CacheFileName, null);
    _FileSystem.FileExists(target).Should().BeTrue();
    using var stream = _FileSystem.OpenReadStream(target);
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    buffer.ToArray().Should().BeEquivalentTo(content);
    _FileSystem.FileExists(new FileDescription(_Storage.Models, $"{model.CacheFileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public async Task FreshDownload_HashMismatch_ThrowsAndLeavesNoFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash);
    var handler = new RecordingHandler(content);

    var act = () => Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
    _FileSystem.FileExists(new FileDescription(_Storage.Models, model.CacheFileName, null)).Should().BeFalse();
    _FileSystem.FileExists(new FileDescription(_Storage.Models, $"{model.CacheFileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public async Task HttpFailure_ThrowsModelDownloadException()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash);
    var handler = new FailingHandler();

    var act = () => Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  [Fact]
  public void RemoveUnusedModels_DeletesEveryFileNoListedModelOwns()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash);
    var older = model with { Sha256 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" };
    string[] unused = ["model.onnx", older.CacheFileName, "retired.onnx", "retired.onnx.download"];
    foreach (var fileName in unused)
    {
      Seed(fileName);
    }

    Downloader(new FailingHandler()).RemoveUnusedModels([model]);

    unused
      .Where(fileName => _FileSystem.FileExists(new FileDescription(_Storage.Models, fileName, null)))
      .Should().BeEmpty();
  }

  [Fact]
  public void RemoveUnusedModels_KeepsListedModelsAndTheirInFlightDownloads()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash);
    var lowercase = model with { Sha256 = UnverifiedHash.ToLowerInvariant() };
    string[] used = [model.CacheFileName, $"{model.CacheFileName}.download"];
    foreach (var fileName in used)
    {
      Seed(fileName);
    }

    Downloader(new FailingHandler()).RemoveUnusedModels([lowercase]);

    used
      .Where(fileName => !_FileSystem.FileExists(new FileDescription(_Storage.Models, fileName, null)))
      .Should().BeEmpty();
  }

  private void Seed(string fileName) =>
    _FileSystem.Copy(new MemoryStream([1, 2, 3]), new FileDescription(_Storage.Models, fileName, null));

  private ModelDownloader Downloader(HttpMessageHandler handler)
  {
    var factory = new Mock<IHttpClientFactory>();
    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
    return new ModelDownloader(factory.Object, _FileSystem, _Storage);
  }

  private sealed class RecordingHandler(byte[] content) : HttpMessageHandler
  {
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
      RequestCount++;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
  }

  private sealed class FailingHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
  }
}
