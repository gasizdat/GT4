using GT4.UI.Dialogs;
using GT4.UI.Pages;

namespace GT4.UI;

internal static class UIRoutes
{
  static UIRoutes()
  {
    AddRoute<ProjectsPage>();
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