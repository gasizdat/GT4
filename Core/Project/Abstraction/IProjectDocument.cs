using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project.Abstraction;

public interface IProjectDocument : IAsyncDisposable, IDisposable
{
  const string MimeType = "application/gt4;storage=sqlite";
  long ProjectRevision { get; }
  ITableData Data { get; }
  IFamilyManager FamilyManager { get; }
  ITableMetadata Metadata { get; }
  ITableNames Names { get; }
  ITablePersonData PersonData { get; }
  IPersonManager PersonManager { get; }
  ITablePersonNames PersonNames { get; }
  ITablePersons Persons { get; }
  ITableRelatives Relatives { get; }
  IRelativesProvider RelativesProvider { get; }

  Task<IDbTransaction> BeginTransactionAsync(CancellationToken token);
  SqliteCommand CreateCommand();
  Task<int> GetLastInsertRowIdAsync(CancellationToken token);
  void UpdateRevision();

  /// <summary>
  /// Executes a non-query command serialized against the single underlying connection.
  /// When the calling async-flow owns the current transaction the command runs directly
  /// (the flow already holds the connection); otherwise it waits for exclusive access.
  /// </summary>
  Task<int> ExecuteNonQueryAsync(SqliteCommand command, CancellationToken token);

  /// <summary>
  /// Executes a scalar command serialized against the single underlying connection.
  /// </summary>
  Task<object?> ExecuteScalarAsync(SqliteCommand command, CancellationToken token);

  /// <summary>
  /// Executes a reader command and projects it via <paramref name="readAsync"/> while the
  /// connection is held exclusively. The callback must only read from the reader; it must not
  /// start other database operations (that would deadlock against the held connection).
  /// </summary>
  Task<TResult> ExecuteReaderAsync<TResult>(SqliteCommand command, Func<SqliteDataReader, Task<TResult>> readAsync, CancellationToken token);
}