#if WINDOWS
using GT4.UI.Abstraction;

namespace GT4.UI.Utils.Platforms.Windows;

// Runs inside MauiApp.Build, before any page exists, so no enhancer of this instance can be
// downloading or holding a model open yet.
internal class ModelCacheSweep(IModelDownloader downloader, IEnumerable<IModelDefinition> definitions) : IMauiInitializeService
{
  public void Initialize(IServiceProvider services)
  {
    var models = definitions.SelectMany(definition => definition.Models);
    downloader.RemoveUnusedModels(models);
  }
}
#endif
