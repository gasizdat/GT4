namespace GT4.UI.Abstraction;

public interface INavigationService
{
  Task GoToAsync(string route);
  Task GoToAsync(string route, bool animate);
  Task GoToAsync(string route, bool animate, Dictionary<string, object> parameters);
}
