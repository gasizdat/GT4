using System.Globalization;

namespace GT4.UI.Components;

// A family card's person chips must each claim the same share of their own row's width as every
// other card's (#394), computed from the row's own live Width rather than a container-level
// SizeChanged handler: SafeBindableLayout.Rebuild reuses recycled chips via BindingContext alone
// and inserts newly-created chips for a grown family without ever re-running such a handler, so
// only a per-chip binding -- wired once at template creation and staying live across BindingContext
// reuse -- reaches every chip a card can end up with.
public class ChipWidthFractionConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is not double rowWidth || rowWidth <= 0)
    {
      return null;
    }

    var onIdiom = (OnIdiom<double>)Application.Current!.Resources["FamilyPersonChipWidthFraction"];
    double fraction = onIdiom;
    return rowWidth * fraction;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
    throw new NotSupportedException();
}
