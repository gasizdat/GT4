using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using Moq;
using Xunit;
using IFileSystem = GT4.Core.Utils.IFileSystem;

namespace GT4.UI.Utils.Tests;

public class ModelDownloaderTests : IDisposable
{
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
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), "irrelevant");
    _FileSystem.Copy(new MemoryStream([1, 2, 3]), new FileDescription(_Storage.Models, model.FileName, null));
    var handler = new RecordingHandler([1, 2, 3]);

    await Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    handler.RequestCount.Should().Be(0);
  }

  [Fact]
  public async Task FreshDownload_HashMatches_PlacesVerifiedFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var hash = Convert.ToHexString(SHA256.HashData(content));
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), hash);
    var handler = new RecordingHandler(content);

    await Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    var target = new FileDescription(_Storage.Models, model.FileName, null);
    _FileSystem.FileExists(target).Should().BeTrue();
    using var stream = _FileSystem.OpenReadStream(target);
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    buffer.ToArray().Should().BeEquivalentTo(content);
    _FileSystem.FileExists(new FileDescription(_Storage.Models, $"{model.FileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public async Task FreshDownload_HashMismatch_ThrowsAndLeavesNoFile()
  {
    byte[] content = [1, 2, 3, 4, 5];
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), "0000000000000000000000000000000000000000000000000000000000000000");
    var handler = new RecordingHandler(content);

    var act = () => Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
    _FileSystem.FileExists(new FileDescription(_Storage.Models, model.FileName, null)).Should().BeFalse();
    _FileSystem.FileExists(new FileDescription(_Storage.Models, $"{model.FileName}.download", null)).Should().BeFalse();
  }

  [Fact]
  public async Task HttpFailure_ThrowsModelDownloadException()
  {
    var model = new ModelDescriptor("model.onnx", new Uri("https://example.test/model.onnx"), "irrelevant");
    var handler = new FailingHandler();

    var act = () => Downloader(handler).EnsureModelAsync(model, CancellationToken.None);

    await act.Should().ThrowAsync<ModelDownloadException>();
  }

  private ModelDownloader Downloader(HttpMessageHandler handler)
  {
    var factory = new Mock<IHttpClientFactory>();
    factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
    return new ModelDownloader(factory.Object, _FileSystem, _Storage);
  }

  private sealed class FixedStorage(DirectoryDescription models) : IStorage
  {
    public DirectoryDescription ProjectsCache => throw new NotSupportedException();
    public DirectoryDescription ProjectsRoot => throw new NotSupportedException();
    public DirectoryDescription AppConfig => throw new NotSupportedException();
    public DirectoryDescription Models { get; } = models;
  }

  // Maps every DirectoryDescription beneath a single temp root, ignoring the OS special folder it
  // names -- mirrors GT4.Core.Project.Tests' DiskFileSystem, duplicated here since that one is
  // internal to a different test assembly.
  private sealed class TempFileSystem(string root) : IFileSystem
  {
    public string ToPath(DirectoryDescription directory) =>
      Path.Combine([root, directory.Root.ToString(), .. directory.Path]);

    public string ToPath(FileDescription file) => Path.Combine(ToPath(file.Directory), file.FileName);

    public bool FileExists(FileDescription file) => File.Exists(ToPath(file));

    public DateTime GetLastWriteTime(FileDescription file) => File.GetLastWriteTime(ToPath(file));

    public long GetFileSize(FileDescription file) => new FileInfo(ToPath(file)).Length;

    public Stream OpenWriteStream(FileDescription file)
    {
      var path = ToPath(file);
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      return File.Create(path);
    }

    public Stream OpenReadStream(FileDescription file) => File.OpenRead(ToPath(file));

    public void Copy(FileDescription from, FileDescription to)
    {
      using var source = OpenReadStream(from);
      Copy(source, to);
    }

    public void Copy(Stream from, FileDescription to)
    {
      using var target = OpenWriteStream(to);
      from.CopyTo(target);
    }

    public void Move(FileDescription from, FileDescription to)
    {
      var targetPath = ToPath(to);
      Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
      File.Move(ToPath(from), targetPath, overwrite: true);
    }

    public void RemoveFile(FileDescription file)
    {
      var path = ToPath(file);
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }

    public void RemoveDirectory(DirectoryDescription directory) => throw new NotSupportedException();

    public FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive) =>
      throw new NotSupportedException();
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
