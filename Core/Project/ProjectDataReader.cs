using Microsoft.Data.Sqlite;
using System.Collections;
using System.Data.Common;

namespace GT4.Core.Project;

/// <summary>
/// A <see cref="DbDataReader"/> returned by <see cref="ProjectCommand.ExecuteReaderAsync"/>. It is
/// used like any reader (<c>await using var reader = ...; while (await reader.ReadAsync(token)) ...</c>)
/// and delegates everything to the underlying <see cref="SqliteDataReader"/>. Its only extra job is to
/// release the connection gate when it is disposed, so the connection is held for the reader's whole
/// lifetime. When the reader was opened inside a transaction the gate is owned by that transaction and
/// is not this reader's to release, so <paramref name="gate"/> is null in that case.
/// </summary>
public sealed class ProjectDataReader : DbDataReader
{
  private readonly SqliteDataReader _Reader;
  private readonly ConnectionGate? _Gate;
  private int _GateReleased;

  internal ProjectDataReader(SqliteDataReader reader, ConnectionGate? gate)
  {
    _Reader = reader;
    _Gate = gate;
  }

  public override int FieldCount => _Reader.FieldCount;
  public override int RecordsAffected => _Reader.RecordsAffected;
  public override bool HasRows => _Reader.HasRows;
  public override bool IsClosed => _Reader.IsClosed;
  public override int Depth => _Reader.Depth;
  public override object this[int ordinal] => _Reader[ordinal];
  public override object this[string name] => _Reader[name];

  public override bool GetBoolean(int ordinal) => _Reader.GetBoolean(ordinal);
  public override byte GetByte(int ordinal) => _Reader.GetByte(ordinal);
  public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
    _Reader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
  public override char GetChar(int ordinal) => _Reader.GetChar(ordinal);
  public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
    _Reader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
  public override string GetDataTypeName(int ordinal) => _Reader.GetDataTypeName(ordinal);
  public override DateTime GetDateTime(int ordinal) => _Reader.GetDateTime(ordinal);
  public override decimal GetDecimal(int ordinal) => _Reader.GetDecimal(ordinal);
  public override double GetDouble(int ordinal) => _Reader.GetDouble(ordinal);
  public override Type GetFieldType(int ordinal) => _Reader.GetFieldType(ordinal);
  public override float GetFloat(int ordinal) => _Reader.GetFloat(ordinal);
  public override Guid GetGuid(int ordinal) => _Reader.GetGuid(ordinal);
  public override short GetInt16(int ordinal) => _Reader.GetInt16(ordinal);
  public override int GetInt32(int ordinal) => _Reader.GetInt32(ordinal);
  public override long GetInt64(int ordinal) => _Reader.GetInt64(ordinal);
  public override string GetName(int ordinal) => _Reader.GetName(ordinal);
  public override int GetOrdinal(string name) => _Reader.GetOrdinal(name);
  public override string GetString(int ordinal) => _Reader.GetString(ordinal);
  public override object GetValue(int ordinal) => _Reader.GetValue(ordinal);
  public override int GetValues(object[] values) => _Reader.GetValues(values);
  public override bool IsDBNull(int ordinal) => _Reader.IsDBNull(ordinal);
  public override bool NextResult() => _Reader.NextResult();
  public override bool Read() => _Reader.Read();
  public override IEnumerator GetEnumerator() => ((IEnumerable)_Reader).GetEnumerator();

  public override Task<bool> ReadAsync(CancellationToken cancellationToken) => _Reader.ReadAsync(cancellationToken);
  public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => _Reader.NextResultAsync(cancellationToken);
  public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => _Reader.IsDBNullAsync(ordinal, cancellationToken);
  public override T GetFieldValue<T>(int ordinal) => _Reader.GetFieldValue<T>(ordinal);
  public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) => _Reader.GetFieldValueAsync<T>(ordinal, cancellationToken);
  public override Stream GetStream(int ordinal) => _Reader.GetStream(ordinal);
  public override TextReader GetTextReader(int ordinal) => _Reader.GetTextReader(ordinal);

  public override void Close() => _Reader.Close();

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _Reader.Dispose();
      ReleaseGate();
    }
  }

  public override async ValueTask DisposeAsync()
  {
    await _Reader.DisposeAsync();
    ReleaseGate();
    GC.SuppressFinalize(this);
  }

  private void ReleaseGate()
  {
    if (_Gate is not null && Interlocked.Exchange(ref _GateReleased, 1) == 0)
    {
      _Gate.Release();
    }
  }
}
