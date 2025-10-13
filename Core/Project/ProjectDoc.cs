namespace GT4.Project;

public class ProjectDoc
{
  public static ProjectDoc CreateNew(FileStream dbFile, string name)
  {
    string path;
    using (dbFile)
    {
      path = dbFile.Name;
      dbFile.Close();
    }

    SQLitePCL.Batteries.Init();
    var connectionString = $"Data Source={path}";//"Data Source=:memory:"
    var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY, Name TEXT)";
    command.ExecuteNonQuery();
    Console.WriteLine("Table 'Users' created or already exists.");

    var ret = new ProjectDoc
    {
    };

    return ret;
  }
}
