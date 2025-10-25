using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public abstract class TableBase
{
  protected TableBase(ProjectDocument document) => Document = document;

  protected static int? TryGetInteger(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

  protected static long? TryGetLong(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

  protected static DateOnly? TryGetDate(SqliteDataReader reader, int ordinal)
  {
    if (reader.IsDBNull(ordinal))
      return null;

    var (date, _) = reader.GetDateTime(ordinal);
    return date;
  }

  protected static TEnum? TryGetEnum<TEnum>(SqliteDataReader reader, int ordinal) where TEnum : struct, Enum =>
    reader.IsDBNull(ordinal) ? null : GetEnum<TEnum>(reader, ordinal);

  protected static TEnum GetEnum<TEnum>(SqliteDataReader reader, int ordinal) where TEnum : Enum =>
    (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt32(ordinal));

  public ProjectDocument Document { get; init; }

  public abstract Task CreateAsync(CancellationToken token);
}
