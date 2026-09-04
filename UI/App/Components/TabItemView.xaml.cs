using System.Windows.Input;

namespace GT4.UI.Components;

// One tab in a tab strip. The look is a three-step ladder — the active tab is the lightest and the
// hovered one sits between it and the rest — which holds in both themes, so the caption keeps a
// single colour per theme and only the chrome changes.
public partial class TabItemView : ContentView
{
  private bool _Hovered;

  public TabItemView()
  {
    InitializeComponent();
    UpdateState();
  }

  public static readonly BindableProperty TextProperty =
    BindableProperty.Create(nameof(Text), typeof(string), typeof(TabItemView));

  public static readonly BindableProperty IsActiveProperty =
    BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(TabItemView), false, propertyChanged: OnIsActiveChanged);

  public static readonly BindableProperty CommandProperty =
    BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(TabItemView));

  public static readonly BindableProperty CommandParameterProperty =
    BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(TabItemView));

  public string? Text
  {
    get => (string?)GetValue(TextProperty);
    set => SetValue(TextProperty, value);
  }

  public bool IsActive
  {
    get => (bool)GetValue(IsActiveProperty);
    set => SetValue(IsActiveProperty, value);
  }

  public ICommand? Command
  {
    get => (ICommand?)GetValue(CommandProperty);
    set => SetValue(CommandProperty, value);
  }

  public object? CommandParameter
  {
    get => GetValue(CommandParameterProperty);
    set => SetValue(CommandParameterProperty, value);
  }

  private static void OnIsActiveChanged(BindableObject obj, object oldValue, object newValue)
  {
    var tab = (TabItemView)obj;
    tab.UpdateState();
  }

  private void OnPointerEntered(object? sender, PointerEventArgs e)
  {
    _Hovered = true;
    UpdateState();
  }

  private void OnPointerExited(object? sender, PointerEventArgs e)
  {
    _Hovered = false;
    UpdateState();
  }

  private void UpdateState()
  {
    var state = IsActive ? "Active" : _Hovered ? "Hovered" : "Inactive";
    VisualStateManager.GoToState(Chrome, state);
  }
}
