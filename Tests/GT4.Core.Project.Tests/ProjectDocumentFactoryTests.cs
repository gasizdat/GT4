using FluentAssertions;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class ProjectDocumentFactoryTests : IAsyncLifetime
{
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"gt4_factory_{Guid.NewGuid():N}.db");
  private readonly ProjectDocumentFactory _factory = new();
  private CancellationToken Token => TestContext.Current.CancellationToken;

  public ValueTask InitializeAsync() => ValueTask.CompletedTask;

  public async ValueTask DisposeAsync()
  {
    foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
    {
      var file = _path + suffix;
      try
      {
        if (File.Exists(file))
        {
          File.Delete(file);
        }
      }
      catch
      {
        // Best-effort temp cleanup.
      }
    }
    await ValueTask.CompletedTask;
  }

  [Fact]
  public async Task CreateNewAsync_CreatesADocumentUsableAfterOpenAsync()
  {
    await using (var created = await _factory.CreateNewAsync(_path, "factory-tests", Token))
    {
      await created.Metadata.SetProjectNameAsync("factory-tests", Token);
    }

    await using var opened = await _factory.OpenAsync(_path, Token);
    (await opened.Metadata.GetProjectNameAsync(Token)).Should().Be("factory-tests");
  }
}
