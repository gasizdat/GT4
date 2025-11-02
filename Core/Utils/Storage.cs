namespace GT4.Core.Utils;

internal class Storage : IStorage
{
  private const string AppId = "GenTree v4";

  public string ApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  public string MyDocuments => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
  public string ProjectsRoot => Path.Combine(MyDocuments, AppId, "local_projects");
}
