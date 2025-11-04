namespace GT4.Core.Utils;

internal class Storage : IStorage
{
  private const string AppId = "{067F098F-8E7B-4DB9-ABEC-C3A70DAB49D9}";
  private const string AppName = "GenTree v4";

  public DirectoryDescription ApplicationData => 
    new DirectoryDescription(Root: Environment.SpecialFolder.ApplicationData, Path: [AppId, "projects_cache"]);
  
  public DirectoryDescription ProjectsRoot => 
    new DirectoryDescription(Root: Environment.SpecialFolder.MyDocuments, Path: [AppName]);
}
