using GT4.UI.Pages;

namespace GT4.UI;

internal static class UIRoutes
{
  static UIRoutes()
  {
    AddRoute<MainPage>();
    AddRoute<ProjectListPage>();
    AddRoute<ProjectPage>();
    AddRoute<FamilyPage>();
    AddRoute<PersonPage>();
    AddRoute<FamilyTreePage>();
    AddRoute<FanChartPage>();
    AddRoute<NamesPage>();
    AddRoute<GalleryPage>();
    AddRoute<SettingsPage>();
    AddRoute<ProjectRevisionsPage>();
    AddRoute<StatisticsPage>();
    AddRoute<KinshipFinderPage>();
    AddRoute<DateCalendarPage>();
    AddRoute<DateToolsPage>();
  }

  public static string GetRoute<TPage>() => $"{typeof(TPage).Namespace}/{typeof(TPage).Name}";

  private static void AddRoute<TPage>()
  {
    var route = GetRoute<TPage>();
    Routing.RegisterRoute(route, typeof(TPage));
  }
}
