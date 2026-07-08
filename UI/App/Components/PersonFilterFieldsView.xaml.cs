using GT4.UI.Utils;

namespace GT4.UI.Components;

/// <summary>
/// The name/sex/marital-status/alive-in-year filter fields, shared by every page that filters a list
/// of persons. Bound to a <see cref="PersonInfoFilter"/>; the host page owns the panel's visibility
/// (fade behavior) and the clear/toggle buttons, since those differ slightly per page's header layout.
/// </summary>
public partial class PersonFilterFieldsView : ContentView
{
  public PersonFilterFieldsView()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty FilterProperty = BindableProperty.Create(
    nameof(Filter),
    typeof(PersonInfoFilter),
    typeof(PersonFilterFieldsView),
    default,
    BindingMode.OneWay);

  public PersonInfoFilter? Filter
  {
    get => (PersonInfoFilter?)GetValue(FilterProperty);
    set => SetValue(FilterProperty, value);
  }
}
