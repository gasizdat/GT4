using GT4.UI.App.Dialogs;
using GT4.UI.App.Pages;

namespace GT4.UI.App;

internal static class UIRoutes
{
  static UIRoutes()
  {
    AddRoute<ProjectsPage>();
    AddRoute<CreateNewProjectDialog>();
    AddRoute<FamiliesPage>();
    AddRoute<FamilyPage>();
  }

  public static string GetRoute<TPage>() => $"{typeof(TPage).Namespace}/{typeof(TPage).Name}";

  private static void AddRoute<TPage>()
  {
    string route = GetRoute<TPage>();
    Routing.RegisterRoute(route, typeof(TPage));
  }
}