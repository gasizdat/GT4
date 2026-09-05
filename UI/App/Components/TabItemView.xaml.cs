using System.Windows.Input;

namespace GT4.UI.Components;

public partial class TabItemView : ContentView
{
  public TabItemView()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty TextProperty =
    BindableProperty.Create(nameof(Text), typeof(string), typeof(TabItemView));

  public static readonly BindableProperty IsActiveProperty =
    BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(TabItemView));

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
}
