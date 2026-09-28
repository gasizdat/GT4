using GT4.UI.Abstraction;

namespace GT4.UI;

internal sealed class NavigationService : INavigationService
{
  public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
  public Task GoToAsync(string route, bool animate) => Shell.Current.GoToAsync(route, animate);

  // Single-use: plain parameters are re-applied to the page whenever Shell returns to it (a modal or
  // subpage pop), resetting it to the state it was first opened with.
  public Task GoToAsync(string route, bool animate, Dictionary<string, object> parameters)
  {
    var singleUse = new ShellNavigationQueryParameters(parameters);
    return Shell.Current.GoToAsync(route, animate, singleUse);
  }
}
