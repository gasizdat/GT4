using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public abstract class TableBase
{
  protected TableBase(IProjectDocument document) => Document = document;

  protected static int? TryGetInteger(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

  protected static long? TryGetLong(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

  protected static string? TryGetString(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

  protected static Date GetDate(SqliteDataReader reader, int dateOrdinal, int dateStatusOrdinal)
  {
    var status = GetEnum<DateStatus>(reader, dateStatusOrdinal);

    var noDate = reader.IsDBNull(dateOrdinal);
    if (noDate)
    {
      return new Date { Status = DateStatus.Unknown };
    }
   
    return Date.Create(reader.GetInt32(dateOrdinal), status);
  }

  protected static Date? TryGetDate(SqliteDataReader reader, int dateOrdinal, int dateStatusOrdinal)
  {
    var noStatus = reader.IsDBNull(dateStatusOrdinal);
    if (noStatus)
      return null;

    return GetDate(reader, dateOrdinal, dateStatusOrdinal);
  }

  protected static TEnum? TryGetEnum<TEnum>(SqliteDataReader reader, int ordinal) where TEnum : struct, Enum =>
    reader.IsDBNull(ordinal) ? null : GetEnum<TEnum>(reader, ordinal);

  protected static TEnum GetEnum<TEnum>(SqliteDataReader reader, int ordinal) where TEnum : Enum =>
    (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt32(ordinal));

  public static readonly int NonCommitedId = 0;

  internal IProjectDocument Document { get; init; }

  internal abstract Task CreateAsync(CancellationToken token);
}
