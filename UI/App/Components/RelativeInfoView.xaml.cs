using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
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
    ShowMoreRelativesCommand = new SafeCommand(OnShowMoreRelativesCommandAsync);
    InitializeComponent();
  }

  private Date? _RelationshipDate => Relative?.Type switch
  {
    RelationshipType.Parent => PersonBirthDate,
    RelationshipType.Child => Relative?.BirthDate,
    _ => Relative?.Date
  };

  public RelativeInfoView()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty ShowMoreButtonProperty = BindableProperty.Create(
    nameof(ShowMoreButton),
    typeof(bool),
    typeof(RelativeInfoView),
    default,
    BindingMode.OneWay);

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

  public static readonly BindableProperty ExandAllRelativesProperty = BindableProperty.Create(
    nameof(ExandAllRelatives),
    typeof(bool),
    typeof(RelativeInfoView),
    false,
    BindingMode.OneWay,
    null,
    OnExandAllRelativesChanged);

  public bool ShowMoreButton
  {
    get => (bool)GetValue(ShowMoreButtonProperty);
    set => SetValue(ShowMoreButtonProperty, value);
  }

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

  public bool ExandAllRelatives
  {
    get => (bool?)GetValue(ExandAllRelativesProperty) ?? false;
    set => SetValue(ExandAllRelativesProperty, value);
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
      view.RefreshView();
      if (view.ExandAllRelatives)
      {
        var value = view.ExandAllRelatives;
        _ = SafeTask.Run(() => view.ToggleAllRelativesAsync(value));
      }
    }
  }

  private static void OnExandAllRelativesChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is RelativeInfoView view && oldValue != newValue)
    {
      var value = newValue is null ? false : (bool)newValue;
      _ = SafeTask.Run(() => view.ToggleAllRelativesAsync(value));
    }
  }

  private async Task ToggleAllRelativesAsync(bool expand)
  {
    if (ShowRelatives != expand)
    {
      // Invoked from Task.Run, so marshal the property change onto the UI thread.
      await Dispatcher.DispatchAsync(() => ShowRelatives = false);
      if (expand)
      {
        await OnShowMoreRelativesCommandAsync();
      }
    }
  }

  private async Task OnShowMoreRelativesCommandAsync()
  {
    var relative = Relative;
    if (relative is null)
    {
      return;
    }

    if (ShowRelatives)
    {
      await Dispatcher.DispatchAsync(() =>
      {
        ShowRelatives = false;
      });
    }
    else
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var relatives = await _CurrentProjectProvider
        .Project
        .RelativesProvider
        .GetRelativeInfosAsync(relative, true, token);
      await Dispatcher.DispatchAsync(() =>
      {
        Relatives = relatives;
        ShowRelatives = true;
      });
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