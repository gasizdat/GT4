using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public abstract class TableBase
{
  protected TableBase(ProjectDocument document) => Document = document;

  protected static int? TryGetInteger(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

  protected static long? TryGetLong(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

  protected static DateTime? TryGetDateTime(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

  protected static TEnum? TryGetEnum<TEnum>(SqliteDataReader reader, int ordinal) where TEnum : Enum =>
    reader.IsDBNull(ordinal) ? default : GetEnum<TEnum>(reader, ordinal);

  protected static TEnum GetEnum<TEnum>(SqliteDataReader reader, int ordinal) where TEnum : Enum =>
    (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt32(ordinal));

  public ProjectDocument Document { get; init; }

  public abstract Task CreateAsync(CancellationToken token);
}
