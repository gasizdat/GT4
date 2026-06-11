using FluentAssertions;
using GT4.Core.Project.Dto;
using System.Collections.Concurrent;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Proves that <see cref="ProjectDocument"/> can be driven from many threads at once without data
/// races, that only one transaction is active at a time, and that transactions behave correctly
/// (commit, rollback-on-dispose, nested savepoints, flow-affinity across thread hops).
/// </summary>
public sealed class ProjectDocumentConcurrencyTests : IAsyncLifetime
{
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"gt4_{Guid.NewGuid():N}.db");
  private ProjectDocument _doc = null!;

  public async ValueTask InitializeAsync()
  {
    _doc = await ProjectDocument.CreateNewAsync(_path, "concurrency-tests", TestContext.Current.CancellationToken);
  }

  public async ValueTask DisposeAsync()
  {
    await _doc.DisposeAsync();
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
  }

  /// <summary>
  /// Runs <paramref name="action"/> <paramref name="count"/> times, each on its own dedicated thread,
  /// releasing them simultaneously for maximum contention. Dedicated threads are used (rather than the
  /// thread pool) because a contended root transaction blocks its thread while it waits for the gate.
  /// </summary>
  private static void RunConcurrently(int count, Func<int, Task> action)
  {
    var exceptions = new ConcurrentBag<Exception>();
    var ready = new CountdownEvent(count);
    using var start = new ManualResetEventSlim(false);
    var threads = new List<Thread>(count);

    for (var i = 0; i < count; i++)
    {
      var index = i;
      var thread = new Thread(() =>
      {
        ready.Signal();
        start.Wait();
        try
        {
          action(index).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
          exceptions.Add(ex);
        }
      })
      {
        IsBackground = true,
        Name = $"concurrency-{index}",
      };
      threads.Add(thread);
      thread.Start();
    }

    ready.Wait();
    start.Set();
    foreach (var thread in threads)
    {
      thread.Join();
    }

    if (!exceptions.IsEmpty)
    {
      throw new AggregateException("One or more concurrent operations failed.", exceptions);
    }
  }

  private async Task<string[]> AllNameValuesAsync(CancellationToken token) =>
    (await _doc.Names.GetNamesByTypeAsync(NameType.AllNames, token))
      .Select(n => n.Value)
      .ToArray();

  [Fact]
  public async Task ConcurrentWrites_FromManyThreads_AllPersistWithoutRacingAsync()
  {
    var token = TestContext.Current.CancellationToken;
    const int writers = 40;

    RunConcurrently(writers, i =>
      _doc.Names.AddNameAsync($"writer_{i}", NameType.FamilyName, null, token));

    var values = await AllNameValuesAsync(token);
    values.Where(v => v.StartsWith("writer_")).Should().HaveCount(writers);
  }

  [Fact]
  public void ConcurrentWrites_ProduceUniqueRowIds()
  {
    var token = TestContext.Current.CancellationToken;

    // last_insert_rowid() integrity: if two inserts interleaved on the shared connection, ids would
    // collide. The gate guarantees each insert + id read is serialized.
    const int writers = 40;
    var ids = new ConcurrentBag<int>();

    RunConcurrently(writers, async i =>
    {
      var name = await _doc.Names.AddNameAsync($"id_{i}", NameType.FamilyName, null, token);
      ids.Add(name.Id);
    });

    ids.Should().HaveCount(writers);
    ids.Should().OnlyHaveUniqueItems();
  }

  [Fact]
  public async Task ConcurrentReadsAndWrites_StayConsistentAsync()
  {
    var token = TestContext.Current.CancellationToken;
    const int total = 60;
    var expectedWrites = Enumerable.Range(0, total).Count(i => i % 3 != 0);

    RunConcurrently(total, i =>
    {
      if (i % 3 == 0)
      {
        return _doc.Names.GetNamesByTypeAsync(NameType.AllNames, token);
      }

      return _doc.Names.AddNameAsync($"mixed_{i}", NameType.FamilyName, null, token);
    });

    var values = await AllNameValuesAsync(token);
    values.Where(v => v.StartsWith("mixed_")).Should().HaveCount(expectedWrites);
  }

  [Fact]
  public async Task OnlyOneRootTransactionIsActiveAtATime()
  {
    var token = TestContext.Current.CancellationToken;
    var order = new ConcurrentQueue<string>();
    var aStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var aMayFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var flowA = Task.Run(async () =>
    {
      using var transaction = await _doc.BeginTransactionAsync(token);
      order.Enqueue("A-begin");
      aStarted.SetResult();
      await aMayFinish.Task;
      order.Enqueue("A-end");
      transaction.Commit();
    }, token);

    await aStarted.Task;

    var flowB = Task.Run(async () =>
    {
      // Must block here until flow A releases the connection.
      using var transaction = await _doc.BeginTransactionAsync(token);
      order.Enqueue("B-begin");
      transaction.Commit();
    }, token);

    // Give flow B time to attempt to start its transaction; it must still be blocked.
    await Task.Delay(250, token);
    order.Should().NotContain("B-begin");

    aMayFinish.SetResult();
    await Task.WhenAll(flowA, flowB);

    order.Should().ContainInOrder("A-begin", "A-end", "B-begin");
  }

  [Fact]
  public async Task Transaction_DisposedWithoutCommit_RollsBackEverything()
  {
    var token = TestContext.Current.CancellationToken;

    using (var transaction = await _doc.BeginTransactionAsync(token))
    {
      await _doc.Names.AddNameAsync("rollback_me", NameType.FamilyName, null, token);
      // Intentionally no Commit(): leaving the using scope must roll back.
    }

    (await AllNameValuesAsync(token)).Should().NotContain("rollback_me");
  }

  [Fact]
  public async Task Transaction_Commit_Persists()
  {
    var token = TestContext.Current.CancellationToken;

    using (var transaction = await _doc.BeginTransactionAsync(token))
    {
      await _doc.Names.AddNameAsync("commit_me", NameType.FamilyName, null, token);
      transaction.Commit();
    }

    (await AllNameValuesAsync(token)).Should().Contain("commit_me");
  }

  [Fact]
  public async Task NestedSavepoint_RollbackInner_KeepsOuterChanges()
  {
    var token = TestContext.Current.CancellationToken;

    await _doc.Names.AddNameAsync("outer_committed", NameType.FamilyName, null, token);

    using (var outer = await _doc.BeginTransactionAsync(token))
    {
      await _doc.Names.AddNameAsync("inside_outer", NameType.FamilyName, null, token);

      using (var savepoint = await _doc.BeginTransactionAsync(token))
      {
        await _doc.Names.AddNameAsync("inside_savepoint", NameType.FamilyName, null, token);
        savepoint.Rollback();
      }

      outer.Commit();
    }

    var values = await AllNameValuesAsync(token);
    values.Should().Contain("outer_committed");
    values.Should().Contain("inside_outer");
    values.Should().NotContain("inside_savepoint");
  }

  [Fact]
  public async Task NestedTransaction_OnSameFlow_DoesNotDeadlock()
  {
    // Fail fast rather than hang if a deadlock regression is introduced.
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(10));

    // A nested BeginTransaction on a flow that already owns the (gate-holding) root must not try to
    // re-acquire the gate; it must complete immediately as a savepoint.
    using var outer = await _doc.BeginTransactionAsync(cts.Token);
    using var nested = await _doc.BeginTransactionAsync(cts.Token);

    await _doc.Names.AddNameAsync("nested_ok", NameType.FamilyName, null, cts.Token);
    nested.Commit();
    outer.Commit();

    (await AllNameValuesAsync(cts.Token)).Should().Contain("nested_ok");
  }

  [Fact]
  public async Task AmbientTransaction_FlowsAcrossThreadHops()
  {
    // If the ambient transaction were keyed on the OS thread instead of the async flow, the work after
    // the thread hop would either deadlock (re-acquiring the held gate) or escape the transaction.
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(10));

    using (var transaction = await _doc.BeginTransactionAsync(cts.Token))
    {
      await Task.Yield();
      await Task.Run(() => Thread.Sleep(1), cts.Token); // force a different thread to resume on

      await _doc.Names.AddNameAsync("after_hop", NameType.FamilyName, null, cts.Token);
      // No commit: rolled back on dispose.
    }

    (await AllNameValuesAsync(cts.Token)).Should().NotContain("after_hop");
  }

  [Fact]
  public async Task ConcurrentTransactionsAndReads_StressTestAsync()
  {
    var token = TestContext.Current.CancellationToken;
    const int total = 80;
    var expectedWrites = Enumerable.Range(0, total).Count(i => i % 4 != 0);

    RunConcurrently(total, async i =>
    {
      if (i % 4 == 0)
      {
        // Pure reads interleaved with writers.
        await _doc.Names.GetNamesByTypeAsync(NameType.AllNames, token);
        await _doc.Persons.GetPersonsAsync(token);
      }
      else
      {
        // Explicit transaction wrapping a write, committed.
        using var transaction = await _doc.BeginTransactionAsync(token);
        await _doc.Names.AddNameAsync($"stress_{i}", NameType.FamilyName, null, token);
        transaction.Commit();
      }
    });

    var values = await AllNameValuesAsync(token);
    values.Where(v => v.StartsWith("stress_")).Should().HaveCount(expectedWrites);
  }

  [Fact]
  public async Task CommandCreatedDuringAnotherFlowsTransaction_RebindsOnExecute()
  {
    // Regression guard: SqliteConnection.CreateCommand() stamps the connection's CURRENT transaction
    // onto the new command. A command created while another flow holds a transaction therefore
    // captures that transaction; by the time it executes (after the gate frees) that transaction has
    // completed. ProjectCommand must rebind at execution time instead of using the stale one.
    var token = TestContext.Current.CancellationToken;
    var aHasBegun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var aMayCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var flowA = Task.Run(async () =>
    {
      using var transaction = await _doc.BeginTransactionAsync(token);
      await _doc.Names.AddNameAsync("from_a", NameType.FamilyName, null, token);
      aHasBegun.SetResult();
      await aMayCommit.Task;
      transaction.Commit();
    }, token);

    await aHasBegun.Task;

    // Created while flow A's transaction is active: the (soon to be completed) transaction is stamped
    // onto this command.
    using var command = _doc.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM Names;";

    // Let flow A commit and release the connection, then execute.
    aMayCommit.SetResult();
    await flowA;

    var count = Convert.ToInt32(await command.ExecuteScalarAsync(token));
    count.Should().BeGreaterThanOrEqualTo(1);
  }

  [Fact]
  public async Task CreateCommand_ExecutesRawSqlDirectly()
  {
    // ProjectCommand is meant to be usable directly: configure it and call its own Execute* methods.
    var token = TestContext.Current.CancellationToken;

    using (var transaction = await _doc.BeginTransactionAsync(token))
    {
      using var insert = _doc.CreateCommand();
      insert.CommandText = "INSERT INTO Names (Value, Type, ParentId) VALUES (@value, @type, NULL);";
      insert.Parameters.AddWithValue("@value", "raw_command");
      insert.Parameters.AddWithValue("@type", (int)NameType.FamilyName);
      (await insert.ExecuteNonQueryAsync(token)).Should().Be(1);
      transaction.Commit();
    }

    using var scalar = _doc.CreateCommand();
    scalar.CommandText = "SELECT COUNT(*) FROM Names WHERE Value = @value;";
    scalar.Parameters.AddWithValue("@value", "raw_command");
    Convert.ToInt32(await scalar.ExecuteScalarAsync(token)).Should().Be(1);

    using var select = _doc.CreateCommand();
    select.CommandText = "SELECT Value FROM Names WHERE Value = @value;";
    select.Parameters.AddWithValue("@value", "raw_command");
    await using var reader = await select.ExecuteReaderAsync(token);
    (await reader.ReadAsync(token)).Should().BeTrue();
    reader.GetString(0).Should().Be("raw_command");
  }

  [Fact]
  public async Task OpenReader_HoldsConnection_UntilDisposed()
  {
    // A standalone reader holds the gate for its whole lifetime; another flow must block until the
    // reader is disposed (which is what releases the gate).
    var token = TestContext.Current.CancellationToken;
    await _doc.Names.AddNameAsync("seed", NameType.FamilyName, null, token);

    var order = new ConcurrentQueue<string>();
    var readerOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var mayDispose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var flowA = Task.Run(async () =>
    {
      using var command = _doc.CreateCommand();
      command.CommandText = "SELECT Value FROM Names;";
      await using var reader = await command.ExecuteReaderAsync(token);
      (await reader.ReadAsync(token)).Should().BeTrue();
      order.Enqueue("reader-open");
      readerOpen.SetResult();
      await mayDispose.Task;
      order.Enqueue("reader-dispose");
      // reader disposed when this scope exits, releasing the gate.
    }, token);

    await readerOpen.Task;

    var flowB = Task.Run(async () =>
    {
      await _doc.Names.AddNameAsync("after_reader", NameType.FamilyName, null, token);
      order.Enqueue("b-wrote");
    }, token);

    await Task.Delay(250, token);
    order.Should().NotContain("b-wrote");

    mayDispose.SetResult();
    await Task.WhenAll(flowA, flowB);

    order.Should().ContainInOrder("reader-open", "reader-dispose", "b-wrote");
    (await AllNameValuesAsync(token)).Should().Contain("after_reader");
  }

  [Fact]
  public async Task ReaderInsideTransaction_DoesNotReleaseTheTransactionGate()
  {
    // A reader opened inside a transaction does not own the gate (the transaction does). Disposing it
    // must NOT free the connection: another flow stays blocked until the transaction itself completes.
    var token = TestContext.Current.CancellationToken;
    var order = new ConcurrentQueue<string>();
    var readerDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var mayCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var flowA = Task.Run(async () =>
    {
      using var transaction = await _doc.BeginTransactionAsync(token);
      await _doc.Names.AddNameAsync("a_in_tx", NameType.FamilyName, null, token);

      using (var command = _doc.CreateCommand())
      {
        command.CommandText = "SELECT COUNT(*) FROM Names;";
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).Should().BeTrue();
      }

      order.Enqueue("a-reader-disposed");
      readerDisposed.SetResult();
      await mayCommit.Task;
      transaction.Commit();
      order.Enqueue("a-committed");
    }, token);

    await readerDisposed.Task;

    var flowB = Task.Run(async () =>
    {
      await _doc.Names.AddNameAsync("b_root", NameType.FamilyName, null, token);
      order.Enqueue("b-wrote");
    }, token);

    // The inner reader is already disposed, but A still holds the transaction, so B must stay blocked.
    await Task.Delay(250, token);
    order.Should().NotContain("b-wrote");

    mayCommit.SetResult();
    await Task.WhenAll(flowA, flowB);

    order.Should().ContainInOrder("a-reader-disposed", "a-committed", "b-wrote");
  }

  [Fact]
  public async Task DataBlob_RoundTripsThroughReader()
  {
    // Exercises the reader's GetStream delegation: TableData.CreateData reads the blob via the wrapper.
    var token = TestContext.Current.CancellationToken;
    var content = new byte[] { 1, 2, 3, 4, 5, 42, 255, 0, 7 };

    var added = await _doc.Data.AddDataAsync(content, "application/octet-stream", DataCategory.PersonPhoto, token);
    var fetched = await _doc.Data.TryGetDataByIdAsync(added.Id, token);

    fetched.Should().NotBeNull();
    fetched!.Content.Should().Equal(content);
    fetched.MimeType.Should().Be("application/octet-stream");
  }
}
