using System.Globalization;

namespace GT4.UI.Components;

// A PageMenuItem's own EditingAction never changes after construction, but ReadOnlyMode.IsEnabled
// does, so the button's visibility must stay a live binding rather than a value PageLayout pushes
// onto the item imperatively -- that would mean PageLayout subscribing to the ReadOnlyMode
// singleton's PropertyChanged directly, which never unsubscribes and leaks every PageLayout for
// the app's lifetime. MAUI's own bindings avoid that (see Animation.IsEnabled above) by holding
// the source weakly, so this stays a converter driven by two Bindings instead.
public class EditingActionVisibleConverter : IMultiValueConverter
{
  public object Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture) =>
    values is not [true, true];

  public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
    throw new NotSupportedException();
}
