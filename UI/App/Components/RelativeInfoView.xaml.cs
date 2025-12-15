using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;

namespace GT4.UI.Components;

public partial class RelativeInfoView : ContentView
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;

  protected RelativeInfoView(IServiceProvider serviceProvider)
  {
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _RelationshipTypeFormatter = serviceProvider.GetRequiredService<IRelationshipTypeFormatter>();
    InitializeComponent();
  }

  private Date? _RelationshipDate => Relative?.Type switch
  {
    RelationshipType.Parent => PersonBirthDate,
    RelationshipType.Child => Relative?.BirthDate,
    _ => Relative?.Date
  };

  public RelativeInfoView()
    : this(ServiceBuilder.DefaultServices)
  {

  }

  public static readonly BindableProperty ShowMoreButtonProperty = BindableProperty.Create(
    nameof(ShowMoreButton),
    typeof(bool),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay,
    null,
    OnRelativeInfoChanged);

  public static readonly BindableProperty PersonBirthDateProperty = BindableProperty.Create(
    nameof(PersonBirthDate),
    typeof(Date),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay,
    null,
    OnRelativeInfoChanged);

  public static readonly BindableProperty RelativeProperty = BindableProperty.Create(
    nameof(Relative),
    typeof(RelativeInfo),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay,
    null,
    OnRelativeInfoChanged);

  public bool ShowMoreButton => (bool)GetValue(ShowMoreButtonProperty);

  public Date? PersonBirthDate => (Date?)GetValue(PersonBirthDateProperty);

  public RelativeInfo? Relative => (RelativeInfo?)GetValue(RelativeProperty);

  public bool ShowDate =>
    _RelationshipDate.HasValue &&
    _RelationshipDate.Value.Status != DateStatus.Unknown &&
    Relative?.Type switch
    {
      RelationshipType.Spose => true,
      RelationshipType.AdoptiveChild => true,
      RelationshipType.StepChild => true,
      RelationshipType.AdoptiveParent => true,
      RelationshipType.StepParent => true,
      RelationshipType.AdoptiveSibling => true,
      RelationshipType.StepSibling => true,
      _ => false
    };

  public string RelationshipDate => _DateFormatter.ToString(_RelationshipDate);

  public string RelationTypeName =>
    Relative is null
    ? string.Empty
    : _RelationshipTypeFormatter.ToString(Relative.Type, Relative.BiologicalSex);

  private static void OnRelativeInfoChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is RelativeInfoView view && oldValue != newValue)
    {
      Utils.RefreshView(view);
    }
  }
}