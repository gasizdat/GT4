namespace GT4.UI;

public interface INavigationService
{
  Task GoToAsync(string route);
  Task GoToAsync(string route, bool animate);
  Task GoToAsync(string route, bool animate, Dictionary<string, object> parameters);
}

internal sealed class RealNavigationService : INavigationService
{
  public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
  public Task GoToAsync(string route, bool animate) => Shell.Current.GoToAsync(route, animate);

  public Task GoToAsync(string route, bool animate, Dictionary<string, object> parameters) =>
    Shell.Current.GoToAsync(route, animate, parameters);
}
