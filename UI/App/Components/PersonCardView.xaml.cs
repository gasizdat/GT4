using GT4.Core.Project.Dto;

namespace GT4.UI.Components;

public partial class PersonCardView : ContentView
{
  public PersonCardView()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty PersonProperty =
    BindableProperty.Create(nameof(Person), typeof(PersonInfo), typeof(PersonCardView), propertyChanged: OnPersonChanged);

  public PersonInfo? Person
  {
    get => (PersonInfo?)GetValue(PersonProperty);
    set => SetValue(PersonProperty, value);
  }

  private static void OnPersonChanged(BindableObject obj, object oldValue, object newValue) =>
    ((PersonCardView)obj).IsVisible = newValue is not null;
}
