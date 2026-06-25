using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using System.Data.Common;

namespace GT4.Core.Project;

public abstract class TableBase : ProjectComponentBase
{
  protected TableBase(IProjectDocument document) : base(document)
  {
  }

  protected static int? TryGetInteger(DbDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

  protected static long? TryGetLong(DbDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

  protected static string? TryGetString(DbDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

  protected static Date GetDate(DbDataReader reader, int dateOrdinal, int dateStatusOrdinal)
  {
    var status = GetEnum<DateStatus>(reader, dateStatusOrdinal);

    var noDate = reader.IsDBNull(dateOrdinal);
    if (noDate)
    {
      return new Date { Status = DateStatus.Unknown };
    }
   
    return Date.Create(reader.GetInt32(dateOrdinal), status);
  }

  protected static Date? TryGetDate(DbDataReader reader, int dateOrdinal, int dateStatusOrdinal)
  {
    var noStatus = reader.IsDBNull(dateStatusOrdinal);
    if (noStatus)
      return null;

    return GetDate(reader, dateOrdinal, dateStatusOrdinal);
  }

  protected static TEnum? TryGetEnum<TEnum>(DbDataReader reader, int ordinal) where TEnum : struct, Enum =>
    reader.IsDBNull(ordinal) ? null : GetEnum<TEnum>(reader, ordinal);

  protected static TEnum GetEnum<TEnum>(DbDataReader reader, int ordinal) where TEnum : Enum =>
    (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt32(ordinal));

  internal abstract Task CreateAsync(CancellationToken token);
}
