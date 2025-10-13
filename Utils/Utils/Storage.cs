namespace GT4.Utils;

internal class Storage : IStorage
{
  private const string AppId = "{2985F7F6-693A-4306-9135-5A955A855F3E}";

  public string ApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  public string ProjectListPath => Path.Combine(ApplicationData, AppId, "projects.json");
}
