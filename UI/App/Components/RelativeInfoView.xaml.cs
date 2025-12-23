using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class RelativeInfoView : ContentView
{
  private const string ExpandSymbol = "🔽";
  private const string CollapseSymbol = "­­­­⏫­";
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateFormatter _DateFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private bool _ShowRelatives = false;
  private ICollection<RelativeInfo>? _Relatives = null;
  private string _MoreBtnName = ExpandSymbol;

  protected RelativeInfoView(IServiceProvider serviceProvider)
  {
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _RelationshipTypeFormatter = serviceProvider.GetRequiredService<IRelationshipTypeFormatter>();
    ShowMoreRelativesCommand = new Command(OnShowMoreRelativesCommandAsync);
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

  public static readonly BindableProperty PersonInfoFrameProperty = BindableProperty.Create(
    nameof(PersonInfoFrame),
    typeof(Rect),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay);

  public bool ShowMoreButton => (bool)GetValue(ShowMoreButtonProperty);

  public Date? PersonBirthDate => (Date?)GetValue(PersonBirthDateProperty);

  public RelativeInfo? Relative => (RelativeInfo?)GetValue(RelativeProperty);

  public Rect PersonInfoFrame => (Rect?)GetValue(PersonInfoFrameProperty) ?? Rect.Zero;

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

  public bool ShowRelatives
  {
    get => _ShowRelatives;
    private set
    {
      if (_ShowRelatives != value)
      {
        _ShowRelatives = value;
        if (_ShowRelatives)
        {
          MoreBtnName = CollapseSymbol;
        }
        else
        {
          Relatives = null;
          MoreBtnName = ExpandSymbol;
        }
        OnPropertyChanged(nameof(ShowRelatives));
      }
    }
  }

  public ICommand ShowMoreRelativesCommand { get; init; }

  public ICollection<RelativeInfo>? Relatives
  {
    get => _Relatives;
    private set
    {
      _Relatives = value;
      OnPropertyChanged(nameof(Relatives));
    }
  }

  public string MoreBtnName
  {
    get => _MoreBtnName;
    private set
    {
      if (_MoreBtnName != value)
      {
        _MoreBtnName = value;
        OnPropertyChanged(nameof(MoreBtnName));
      }
    }
  }

  private static void OnRelativeInfoChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is RelativeInfoView view && oldValue != newValue)
    {
      view.ShowRelatives = false;
      Utils.RefreshView(view);
    }
  }

  private async void OnShowMoreRelativesCommandAsync()
  {
    var relative = Relative;
    if (relative is null)
    {
      return;
    }

    if (ShowRelatives)
    {
      ShowRelatives = false;
    }
    else
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var relatives = await _CurrentProjectProvider
        .Project
        .RelativesProvider
        .GetRelativeInfosAsync(relative, true, token);

      Relatives = relatives;
      ShowRelatives = true;
    }
  }

  private void PersonInfoViewSizeChanged(object sender, EventArgs e)
  {
    if (sender is PersonInfoView personInfoView)
    {
      var frame = personInfoView.Frame;
      SetValue(PersonInfoFrameProperty, frame);
      OnPropertyChanged(nameof(PersonInfoFrame));
    }
  }
}