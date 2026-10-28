#if WINDOWS
using GT4.UI.Abstraction;

namespace GT4.UI.Utils.Platforms.Windows;

// Safe to delete here: MauiApp.Build runs this before any page can start a download.
internal class ModelCacheSweep(IModelDownloader downloader, IEnumerable<IModelDefinition> definitions) : IMauiInitializeService
{
  public void Initialize(IServiceProvider services)
  {
    var models = definitions.SelectMany(definition => definition.Models);
    downloader.RemoveUnusedModels(models);
  }
}
#endif
