namespace GT4.Core.Utils;

internal sealed class AppConfigurationProvider : FlatJsonConfigurationProvider
{
  private readonly IStorage _Storage;

  public AppConfigurationProvider(IFileSystem fileSystem, IStorage storage) : base(fileSystem)
  {
    _Storage = storage;
  }

  public override string Name => WellKnownActiveConfigurations.AppConfig;

  protected override FileDescription File =>
    new FileDescription(_Storage.AppConfig, "appconfig.json", System.Net.Mime.MediaTypeNames.Text.Plain);
}
