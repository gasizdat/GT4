namespace GT4.Project;

public class ProjectDoc
{
  public static async Task<ProjectDoc> CreateNewAsync(FileStream dbFile, string name)
  {
    string path;
    using (dbFile)
    {
      path = dbFile.Name;
      dbFile.Close();
    }

    SQLitePCL.Batteries.Init();
    var connectionString = $"Data Source={path}";//"Data Source=:memory:"
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
    await connection.OpenAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY, Name TEXT)";
    command.ExecuteNonQuery();
    Console.WriteLine("Table 'Users' created or already exists.");

    var ret = new ProjectDoc
    {
    };

    await connection.CloseAsync();
    return ret;
  }
}
