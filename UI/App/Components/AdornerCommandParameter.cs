namespace GT4.UI.App.Components;

public class AdornerCommandParameter : BindableObject
{
  public static readonly BindableProperty ElementProperty =
          BindableProperty.Create(nameof(Element), typeof(object), typeof(AdornerCommandParameter));
  public static readonly BindableProperty CommandNameProperty =
          BindableProperty.Create(nameof(CommandName), typeof(string), typeof(AdornerCommandParameter));

  public object Element
  {
    get => GetValue(ElementProperty);
    set => SetValue(ElementProperty, value);
  }

  public string CommandName
  {
    get => (string)GetValue(CommandNameProperty);
    set => SetValue(CommandNameProperty, value);
  }
}

