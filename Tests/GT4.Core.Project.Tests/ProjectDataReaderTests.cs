using FluentAssertions;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Exercises the delegating accessors of <see cref="ProjectDataReader"/> by reading a single row of
/// mixed SQLite types through it. Covers both the synchronous dispose path and the async one. Blob
/// streaming is covered separately by the concurrency suite's <c>DataBlob_RoundTripsThroughReader</c>.
/// </summary>
public sealed class ProjectDataReaderTests : IAsyncLifetime
{
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"gt4_rdr_{Guid.NewGuid():N}.db");
  private ProjectDocument _doc = null!;
  private CancellationToken Token => TestContext.Current.CancellationToken;

  private const string Guid1 = "d3630d8a-4e3e-4f1a-9b2c-1a2b3c4d5e6f";

  // num=42, txt='hello', dbl=3.5, dt='2020-01-02 03:04:05', guid, null.
  // Aliases are double-quoted so none collides with a SQLite keyword.
  private const string Query =
    "SELECT 42 AS \"num\", 'hello' AS \"txt\", 3.5 AS \"dbl\", " +
    "'2020-01-02 03:04:05' AS \"dt\", '" + Guid1 + "' AS \"guid\", NULL AS \"nothing\";";

  public async ValueTask InitializeAsync()
  {
    _doc = await ProjectDocument.CreateNewAsync(_path, "reader-tests", TestContext.Current.CancellationToken);
  }

  public async ValueTask DisposeAsync()
  {
    await _doc.DisposeAsync();
    foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
    {
      try { File.Delete(_path + suffix); } catch { /* best-effort */ }
    }
  }

  [Fact]
  public async Task SyncAccessors_DelegateToUnderlyingReader()
  {
    using var command = _doc.CreateCommand();
    command.CommandText = Query;

    using var reader = await command.ExecuteReaderAsync(Token);

    reader.FieldCount.Should().Be(6);
    reader.HasRows.Should().BeTrue();
    reader.IsClosed.Should().BeFalse();
    reader.Depth.Should().Be(0);
    _ = reader.RecordsAffected;

    reader.Read().Should().BeTrue();

    // Integer column read through the various numeric accessors.
    reader.GetInt32(0).Should().Be(42);
    reader.GetInt64(0).Should().Be(42);
    reader.GetInt16(0).Should().Be(42);
    reader.GetByte(0).Should().Be(42);
    reader.GetBoolean(0).Should().BeTrue();
    reader.GetDecimal(0).Should().Be(42m);
    reader.GetDouble(2).Should().Be(3.5);
    reader.GetFloat(2).Should().Be(3.5f);

    // Text, datetime, guid.
    reader.GetString(1).Should().Be("hello");
    reader.GetDateTime(3).Should().Be(new DateTime(2020, 1, 2, 3, 4, 5));
    reader.GetGuid(4).Should().Be(Guid.Parse(Guid1));

    // Metadata / lookups.
    reader.GetName(0).Should().Be("num");
    reader.GetOrdinal("txt").Should().Be(1);
    reader.GetFieldType(1).Should().Be(typeof(string));
    reader.GetDataTypeName(0).Should().NotBeNull();
    reader["txt"].Should().Be("hello");
    reader[0].Should().Be(42L);

    // Generic + null handling.
    reader.GetFieldValue<long>(0).Should().Be(42);
    reader.IsDBNull(5).Should().BeTrue();
    reader.IsDBNull(0).Should().BeFalse();

    var values = new object[6];
    reader.GetValues(values).Should().Be(6);
    values[1].Should().Be("hello");

    reader.GetValue(1).Should().Be("hello");

    reader.NextResult().Should().BeFalse();
    // The sync `using` above disposes through Dispose(bool), releasing the gate.
  }

  /// <summary>
  /// Confirms the self-deadlock risk documented on <see cref="ProjectCommand.ExecuteReaderAsync"/>:
  /// a reader opened outside a transaction holds the connection gate until disposed, so a second
  /// gated call on the *same flow* can never be released by anyone and must be cancelled externally.
  /// </summary>
  [Fact]
  public async Task NestedReadOnSameFlow_WithoutTransaction_SelfDeadlocks()
  {
    using var outerCommand = _doc.CreateCommand();
    outerCommand.CommandText = Query;
    await using var outerReader = await outerCommand.ExecuteReaderAsync(Token);
    (await outerReader.ReadAsync(Token)).Should().BeTrue();

    using var innerCommand = _doc.CreateCommand();
    innerCommand.CommandText = Query;
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

    var act = () => innerCommand.ExecuteReaderAsync(cts.Token);

    await act.Should().ThrowAsync<OperationCanceledException>();
  }

  [Fact]
  public async Task AsyncAccessors_AndAsyncDispose()
  {
    using var command = _doc.CreateCommand();
    command.CommandText = Query;

    await using var reader = await command.ExecuteReaderAsync(Token);

    (await reader.ReadAsync(Token)).Should().BeTrue();
    (await reader.GetFieldValueAsync<string>(1, Token)).Should().Be("hello");
    (await reader.IsDBNullAsync(5, Token)).Should().BeTrue();
    (await reader.NextResultAsync(Token)).Should().BeFalse();
    // Disposed via `await using`, exercising DisposeAsync + the gate release.
  }
}
