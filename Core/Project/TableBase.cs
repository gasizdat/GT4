using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public abstract class TableBase
{
  protected TableBase(ProjectDocument document) => Document = document;

  protected static int? TryGetInteger(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

  public ProjectDocument Document { get; init; }

  public abstract Task CreateAsync();
}
