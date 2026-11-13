using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ModelDownloaderTests : IDisposable
{
  private const string UnverifiedHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

  private readonly FixtureModelCache _Cache = new();

  public void Dispose() => _Cache.Dispose();

  [Fact]
  public async Task AlreadyPresentModel_NoHttpCallMade()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    Seed(model.CacheFileName);
    var handler = new RecordingHandler([1, 2, 3]);

    await _Cache.Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    handler.RequestCount.Should().Be(0);
  }

  [Fact]
  public void IsDownloaded_OnlyForAFileUnderTheModelsCacheName()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var downloader = _Cache.Downloader(new FailingHandler());
    Seed(model.FileName);

    var beforeDownload = downloader.IsDownloaded(model);
    Seed(model.CacheFileName);
    var afterDownload = downloader.IsDownloaded(model);

    beforeDownload.Should().BeFalse();
    afterDownload.Should().BeTrue();
  }

  [Fact]
  public async Task SameFileNameWithNewHash_DownloadsInsteadOfReusingTheOldFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var hash = Convert.ToHexString(SHA256.HashData(content));
    var stale = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var model = stale with { Sha256 = hash };
    Seed(stale.CacheFileName);
    var handler = new RecordingHandler(content);

    await _Cache.Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    handler.RequestCount.Should().Be(1);
    _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, model.CacheFileName, null)).Should().BeTrue();
  }

  [Fact]
  public async Task FreshDownload_HashMatches_PlacesVerifiedFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var hash = Convert.ToHexString(SHA256.HashData(content));
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), hash, "Model", "MIT", 5);
    var handler = new RecordingHandler(content);

    await _Cache.Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    var target = new FileDescription(_Cache.Models, model.CacheFileName, null);
    _Cache.FileSystem.FileExists(target).Should().BeTrue();
    using var stream = _Cache.FileSystem.OpenReadStream(target);
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    buffer.ToArray().Should().BeEquivalentTo(content);
    _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, $"{model.CacheFileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public async Task FreshDownload_HashMismatch_ThrowsAndLeavesNoFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var handler = new RecordingHandler(content);

    var act = () => _Cache.Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
    _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, model.CacheFileName, null)).Should().BeFalse();
    _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, $"{model.CacheFileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public async Task HttpFailure_ThrowsModelDownloadException()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var handler = new FailingHandler();

    var act = () => _Cache.Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  [Fact]
  public async Task HttpTimeout_ThrowsModelDownloadException()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var handler = new TimingOutHandler();

    var act = () => _Cache.Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    var thrown = await act.Should().ThrowAsync<ModelDownloadException>();
    thrown.WithInnerException<TaskCanceledException>();
  }

  [Fact]
  public async Task CancelledMidDownload_StopsAndLeavesNoFile()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var body = new StalledStream();
    using var cancellation = new CancellationTokenSource();

    var download = _Cache.Downloader(new StreamHandler(body)).EnsureModelAsync(model, cancellation.Token);
    await body.Reading;
    cancellation.Cancel();

    var act = () => download;
    await act.Should().ThrowWithinAsync<OperationCanceledException>(TimeSpan.FromSeconds(5));
    _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, model.CacheFileName, null)).Should().BeFalse();
    _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, $"{model.CacheFileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public void RemoveUnusedModels_DeletesEveryFileNoListedModelOwns()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var older = model with { Sha256 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" };
    string[] unused = ["model.onnx", older.CacheFileName, "retired.onnx", "retired.onnx.download"];
    foreach (var fileName in unused)
    {
      Seed(fileName);
    }

    _Cache.Downloader(new FailingHandler()).RemoveUnusedModels([model]);

    unused
      .Where(fileName => _Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, fileName, null)))
      .Should().BeEmpty();
  }

  [Fact]
  public void RemoveUnusedModels_KeepsListedModelsAndTheirInFlightDownloads()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), UnverifiedHash, "Model", "MIT", 3);
    var lowercase = model with { Sha256 = UnverifiedHash.ToLowerInvariant() };
    string[] used = [model.CacheFileName, $"{model.CacheFileName}.download"];
    foreach (var fileName in used)
    {
      Seed(fileName);
    }

    _Cache.Downloader(new FailingHandler()).RemoveUnusedModels([lowercase]);

    used
      .Where(fileName => !_Cache.FileSystem.FileExists(new FileDescription(_Cache.Models, fileName, null)))
      .Should().BeEmpty();
  }

  private void Seed(string fileName) =>
    _Cache.FileSystem.Copy(new MemoryStream([1, 2, 3]), new FileDescription(_Cache.Models, fileName, null));

  private sealed class RecordingHandler(byte[] content) : HttpMessageHandler
  {
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
      RequestCount++;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
  }

  private sealed class TimingOutHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      Task.FromException<HttpResponseMessage>(new TaskCanceledException());
  }

  private sealed class StreamHandler(Stream body) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
  }

  // A body that stalls far longer than the test waits, unless its read is cancelled.
  private sealed class StalledStream : Stream
  {
    private readonly TaskCompletionSource _Reading = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reading => _Reading.Task;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
    {
      _Reading.TrySetResult();
      await Task.Delay(TimeSpan.FromSeconds(30), token);
      return 0;
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().Result;
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
