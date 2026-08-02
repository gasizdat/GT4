using GT4.UI.Abstraction;

namespace GT4.UI;

internal sealed class NavigationService : INavigationService
{
  public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
  public Task GoToAsync(string route, bool animate) => Shell.Current.GoToAsync(route, animate);

  public Task GoToAsync(string route, bool animate, Dictionary<string, object> parameters) =>
    Shell.Current.GoToAsync(route, animate, parameters);
}
