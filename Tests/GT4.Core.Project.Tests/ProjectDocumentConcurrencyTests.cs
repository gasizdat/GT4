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
    _doc = await ProjectDocument.CreateNewAsync(_path, "concurrency-tests", CancellationToken.None);
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

  private async Task<string[]> AllNameValuesAsync() =>
    (await _doc.Names.GetNamesByTypeAsync(NameType.AllNames, CancellationToken.None))
      .Select(n => n.Value)
      .ToArray();

  [Fact]
  public async Task ConcurrentWrites_FromManyThreads_AllPersistWithoutRacingAsync()
  {
    const int writers = 40;

    RunConcurrently(writers, i =>
      _doc.Names.AddNameAsync($"writer_{i}", NameType.FamilyName, null, CancellationToken.None));

    var values = await AllNameValuesAsync();
    values.Where(v => v.StartsWith("writer_")).Should().HaveCount(writers);
  }

  [Fact]
  public void ConcurrentWrites_ProduceUniqueRowIds()
  {
    // last_insert_rowid() integrity: if two inserts interleaved on the shared connection, ids would
    // collide. The gate guarantees each insert + id read is serialized.
    const int writers = 40;
    var ids = new ConcurrentBag<int>();

    RunConcurrently(writers, async i =>
    {
      var name = await _doc.Names.AddNameAsync($"id_{i}", NameType.FamilyName, null, CancellationToken.None);
      ids.Add(name.Id);
    });

    ids.Should().HaveCount(writers);
    ids.Should().OnlyHaveUniqueItems();
  }

  [Fact]
  public async Task ConcurrentReadsAndWrites_StayConsistentAsync()
  {
    const int total = 60;
    var expectedWrites = Enumerable.Range(0, total).Count(i => i % 3 != 0);

    RunConcurrently(total, i =>
    {
      if (i % 3 == 0)
      {
        return _doc.Names.GetNamesByTypeAsync(NameType.AllNames, CancellationToken.None);
      }

      return _doc.Names.AddNameAsync($"mixed_{i}", NameType.FamilyName, null, CancellationToken.None);
    });

    var values = await AllNameValuesAsync();
    values.Where(v => v.StartsWith("mixed_")).Should().HaveCount(expectedWrites);
  }

  [Fact]
  public async Task OnlyOneRootTransactionIsActiveAtATime()
  {
    var order = new ConcurrentQueue<string>();
    var aStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var aMayFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
   
    var flowA = Task.Run(async () =>
    {
      using var transaction = await _doc.BeginTransactionAsync(CancellationToken.None);
      order.Enqueue("A-begin");
      aStarted.SetResult();
      await aMayFinish.Task;
      order.Enqueue("A-end");
      transaction.Commit();
    });

    await aStarted.Task;

    var flowB = Task.Run(async () =>
    {
      // Must block here until flow A releases the connection.
      using var transaction = await _doc.BeginTransactionAsync(CancellationToken.None);
      order.Enqueue("B-begin");
      transaction.Commit();
    });

    // Give flow B time to attempt to start its transaction; it must still be blocked.
    await Task.Delay(250);
    order.Should().NotContain("B-begin");

    aMayFinish.SetResult();
    await Task.WhenAll(flowA, flowB);

    order.Should().ContainInOrder("A-begin", "A-end", "B-begin");
  }

  [Fact]
  public async Task Transaction_DisposedWithoutCommit_RollsBackEverything()
  {
    using (var transaction = await _doc.BeginTransactionAsync(CancellationToken.None))
    {
      await _doc.Names.AddNameAsync("rollback_me", NameType.FamilyName, null, CancellationToken.None);
      // Intentionally no Commit(): leaving the using scope must roll back.
    }

    (await AllNameValuesAsync()).Should().NotContain("rollback_me");
  }

  [Fact]
  public async Task Transaction_Commit_Persists()
  {
    using (var transaction = await _doc.BeginTransactionAsync(CancellationToken.None))
    {
      await _doc.Names.AddNameAsync("commit_me", NameType.FamilyName, null, CancellationToken.None);
      transaction.Commit();
    }

    (await AllNameValuesAsync()).Should().Contain("commit_me");
  }

  [Fact]
  public async Task NestedSavepoint_RollbackInner_KeepsOuterChanges()
  {
    await _doc.Names.AddNameAsync("outer_committed", NameType.FamilyName, null, CancellationToken.None);

    using (var outer = await _doc.BeginTransactionAsync(CancellationToken.None))
    {
      await _doc.Names.AddNameAsync("inside_outer", NameType.FamilyName, null, CancellationToken.None);

      using (var savepoint = await _doc.BeginTransactionAsync(CancellationToken.None))
      {
        await _doc.Names.AddNameAsync("inside_savepoint", NameType.FamilyName, null, CancellationToken.None);
        savepoint.Rollback();
      }

      outer.Commit();
    }

    var values = await AllNameValuesAsync();
    values.Should().Contain("outer_committed");
    values.Should().Contain("inside_outer");
    values.Should().NotContain("inside_savepoint");
  }

  [Fact]
  public async Task NestedTransaction_OnSameFlow_DoesNotDeadlock()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    // A nested BeginTransaction on a flow that already owns the (gate-holding) root must not try to
    // re-acquire the gate; it must complete immediately as a savepoint.
    using var outer = await _doc.BeginTransactionAsync(cts.Token);
    using var nested = await _doc.BeginTransactionAsync(cts.Token);

    await _doc.Names.AddNameAsync("nested_ok", NameType.FamilyName, null, cts.Token);
    nested.Commit();
    outer.Commit();

    (await AllNameValuesAsync()).Should().Contain("nested_ok");
  }

  [Fact]
  public async Task AmbientTransaction_FlowsAcrossThreadHops()
  {
    // If the ambient transaction were keyed on the OS thread instead of the async flow, the work after
    // the thread hop would either deadlock (re-acquiring the held gate) or escape the transaction.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    using (var transaction = await _doc.BeginTransactionAsync(cts.Token))
    {
      await Task.Yield();
      await Task.Run(() => Thread.Sleep(1), cts.Token); // force a different thread to resume on

      await _doc.Names.AddNameAsync("after_hop", NameType.FamilyName, null, cts.Token);
      // No commit: rolled back on dispose.
    }

    (await AllNameValuesAsync()).Should().NotContain("after_hop");
  }
   
  [Fact]
  public async Task ConcurrentTransactionsAndReads_StressTestAsync()
  {
    const int total = 80;
    var expectedWrites = Enumerable.Range(0, total).Count(i => i % 4 != 0);

    RunConcurrently(total, async i =>
    {
      if (i % 4 == 0)
      {
        // Pure reads interleaved with writers.
        await _doc.Names.GetNamesByTypeAsync(NameType.AllNames, CancellationToken.None);
        await _doc.Persons.GetPersonsAsync(CancellationToken.None);
      }
      else
      {
        // Explicit transaction wrapping a write, committed.
        using var transaction = await _doc.BeginTransactionAsync(CancellationToken.None);
        await _doc.Names.AddNameAsync($"stress_{i}", NameType.FamilyName, null, CancellationToken.None);
        transaction.Commit();
      }
    });

    var values = await AllNameValuesAsync();
    values.Where(v => v.StartsWith("stress_")).Should().HaveCount(expectedWrites);
  }
}
