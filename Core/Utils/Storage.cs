namespace GT4.Core.Utils;

internal class Storage : IStorage
{
  private const string AppId = "{2985F7F6-693A-4306-9135-5A955A855F3E}";

  public string ApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  public string MyDocuments => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
  public string ProjectsRoot => Path.Combine(MyDocuments, AppId, "local_projects");
}
