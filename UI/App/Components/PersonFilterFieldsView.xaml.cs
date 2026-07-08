using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// The name/sex/marital-status/alive-in-year filter fields plus the clear/toggle buttons and the
/// fading panel wrapper, shared by every page that filters a list of persons. Bound to a
/// <see cref="PersonInfoFilter"/> and to the host page's own filters-panel state/command, so pages
/// only forward properties rather than duplicating this markup.
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

  public static readonly BindableProperty HeaderTextProperty = BindableProperty.Create(
    nameof(HeaderText),
    typeof(string),
    typeof(PersonFilterFieldsView),
    default(string),
    BindingMode.OneWay);

  public string? HeaderText
  {
    get => (string?)GetValue(HeaderTextProperty);
    set => SetValue(HeaderTextProperty, value);
  }

  public static readonly BindableProperty CommandProperty = BindableProperty.Create(
    nameof(Command),
    typeof(ICommand),
    typeof(PersonFilterFieldsView),
    default(ICommand),
    BindingMode.OneWay);

  public ICommand? Command
  {
    get => (ICommand?)GetValue(CommandProperty);
    set => SetValue(CommandProperty, value);
  }

  public static readonly BindableProperty IsAnyFilterActiveProperty = BindableProperty.Create(
    nameof(IsAnyFilterActive),
    typeof(bool),
    typeof(PersonFilterFieldsView),
    false,
    BindingMode.OneWay);

  public bool IsAnyFilterActive
  {
    get => (bool)GetValue(IsAnyFilterActiveProperty);
    set => SetValue(IsAnyFilterActiveProperty, value);
  }

  public static readonly BindableProperty IsFiltersVisibleProperty = BindableProperty.Create(
    nameof(IsFiltersVisible),
    typeof(bool),
    typeof(PersonFilterFieldsView),
    false,
    BindingMode.OneWay);

  public bool IsFiltersVisible
  {
    get => (bool)GetValue(IsFiltersVisibleProperty);
    set => SetValue(IsFiltersVisibleProperty, value);
  }

  public static readonly BindableProperty ToggleFiltersButtonNameProperty = BindableProperty.Create(
    nameof(ToggleFiltersButtonName),
    typeof(string),
    typeof(PersonFilterFieldsView),
    default(string),
    BindingMode.OneWay);

  public string? ToggleFiltersButtonName
  {
    get => (string?)GetValue(ToggleFiltersButtonNameProperty);
    set => SetValue(ToggleFiltersButtonNameProperty, value);
  }
}
