using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

public partial class PersonCardView : ContentView
{
  private readonly IBirthCohortFormatter _BirthCohortFormatter;

  protected PersonCardView(IServiceProvider serviceProvider)
  {
    _BirthCohortFormatter = serviceProvider.GetRequiredService<IBirthCohortFormatter>();
    InitializeComponent();
  }

  public PersonCardView()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty PersonProperty =
    BindableProperty.Create(nameof(Person), typeof(PersonInfo), typeof(PersonCardView), propertyChanged: OnPersonChanged);

  public PersonInfo? Person
  {
    get => (PersonInfo?)GetValue(PersonProperty);
    set => SetValue(PersonProperty, value);
  }

  public string? BirthCohort => Person is null ? null : _BirthCohortFormatter.ToString(Person.BirthDate);

  public bool HasBirthCohort => BirthCohort is not null;

  private static void OnPersonChanged(BindableObject obj, object oldValue, object newValue)
  {
    var card = (PersonCardView)obj;
    card.IsVisible = newValue is not null;
    card.OnPropertyChanged(nameof(BirthCohort));
    card.OnPropertyChanged(nameof(HasBirthCohort));
  }
}
