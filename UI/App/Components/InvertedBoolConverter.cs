using System.Globalization;

namespace GT4.UI.Components;

public class InvertedBoolConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
    value is bool boolValue ? !boolValue : value;

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
    Convert(value, targetType, parameter, culture);
}
