using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// Displays a single relative — relation label, person card and the relationship date. This is a leaf
/// view with no expansion of its own; the relatives tree is flattened and the per-row chrome (indent,
/// connector lines, the "more" button) lives in <see cref="RelativeRowView"/>. It is also reused
/// standalone wherever a lone relative needs rendering.
/// </summary>
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

  public RelativeInfoView()
    : this(GT4Services.Provider)
  {

  }

  private Date? _RelationshipDate => Relative?.Type switch
  {
    RelationshipType.Parent => PersonBirthDate,
    RelationshipType.Child => Relative?.BirthDate,
    _ => Relative?.Date
  };

  public static readonly BindableProperty PersonBirthDateProperty = BindableProperty.Create(
    nameof(PersonBirthDate),
    typeof(Date),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay);

  public static readonly BindableProperty RelativeProperty = BindableProperty.Create(
    nameof(Relative),
    typeof(RelativeInfo),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay,
    null,
    OnRelativeInfoChanged);

  public static readonly BindableProperty PersonInfoFrameProperty = BindableProperty.Create(
    nameof(PersonInfoFrame),
    typeof(Rect),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay);

  public static readonly BindableProperty NameFormatProperty = BindableProperty.Create(
    nameof(NameFormat),
    typeof(NameFormat),
    typeof(RelativeInfoView),
    NameFormat.CommonPersonName,
    BindingMode.OneWay);

  public static readonly BindableProperty SelectCommandProperty = BindableProperty.Create(
    nameof(SelectCommand),
    typeof(ICommand),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay);

  public Date? PersonBirthDate
  {
    get => (Date?)GetValue(PersonBirthDateProperty);
    set => SetValue(PersonBirthDateProperty, value);
  }

  public RelativeInfo? Relative
  {
    get => (RelativeInfo?)GetValue(RelativeProperty);
    set => SetValue(RelativeProperty, value);
  }

  public Rect PersonInfoFrame
  {
    get => (Rect?)GetValue(PersonInfoFrameProperty) ?? Rect.Zero;
    set => SetValue(PersonInfoFrameProperty, value);
  }

  public NameFormat NameFormat
  {
    get => (NameFormat?)GetValue(NameFormatProperty) ?? NameFormat.CommonPersonName;
    set => SetValue(NameFormatProperty, value);
  }

  public ICommand? SelectCommand
  {
    get => (ICommand?)GetValue(SelectCommandProperty);
    set => SetValue(SelectCommandProperty, value);
  }

  public bool ShowDate =>
    _RelationshipDate.HasValue &&
    _RelationshipDate.Value.Status != DateStatus.Unknown &&
    Relative?.Type switch
    {
      RelationshipType.Spouse => true,
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
    : _RelationshipTypeFormatter.ToString(
      Relative.Type,
      Relative.BiologicalSex,
      Relative.Generation,
      Relative.Consanguinity);

  private static void OnRelativeInfoChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is RelativeInfoView view && oldValue != newValue)
    {
      view.RefreshView();
    }
  }

  private void PersonInfoViewSizeChanged(object sender, EventArgs e)
  {
    if (sender is PersonInfoView personInfoView)
    {
      PersonInfoFrame = personInfoView.Frame;
    }
  }
}
