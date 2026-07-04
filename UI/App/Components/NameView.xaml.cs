using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class NameView : ContentView
{
  private const string ExpandSymbol = "🔽";
  private const string CollapseSymbol = "­­­­⏫­";
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IAlertService _AlertService;
  private bool _ShowPersons = false;
  private ICollection<PersonInfo>? _Persons = null;
  private string _MoreBtnName = ExpandSymbol;

  protected NameView(IServiceProvider serviceProvider)
  {
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _AlertService = serviceProvider.GetRequiredService<IAlertService>();
    TogglePersonsCommand = new SafeCommand(OnTogglePersonsAsync, _AlertService);
    _PersonInfoComparer = serviceProvider.GetKeyedService<IComparer<PersonInfo>>(PersonNamesFormat) ??
                          serviceProvider.GetRequiredService<IComparer<PersonInfo>>();

    InitializeComponent();
  }

  public NameView()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty NameProperty = BindableProperty.Create(
    nameof(Name),
    typeof(Name),
    typeof(NameView),
    default,
    BindingMode.OneWay,
    null,
    OnNameChanged);

  public Name? Name => (Name?)GetValue(NameProperty);

  public ICommand TogglePersonsCommand { get; init; }

  public NameFormat PersonNamesFormat => NameFormat.FullPersonName;

  public ICollection<PersonInfo>? Persons
  {
    get => _Persons;
    private set
    {
      _Persons = value;
      OnPropertyChanged(nameof(Persons));
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

  public bool ShowPersons
  {
    get => _ShowPersons;
    set
    {
      if (_ShowPersons != value)
      {
        _ShowPersons = value;
        MoreBtnName = _ShowPersons ? CollapseSymbol : ExpandSymbol;
        OnPropertyChanged(nameof(ShowPersons));
      }
    }
  }

  private static void OnNameChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is NameView view && oldValue != newValue)
    {
      view.ShowPersons = false;
      view.RefreshView();
    }
  }

  private async Task OnTogglePersonsAsync()
  {
    if(!MainThread.IsMainThread)
    {
      throw new ApplicationException($"{nameof(OnTogglePersonsAsync)} should be called on the main thread.");
    }

    if (!ShowPersons && Name is not null)
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var persons = await _CurrentProjectProvider
        .Project
        .PersonManager
        .GetPersonInfosByNameAsync(Name, true, token);

      Persons = persons
          .OrderBy(item => item, _PersonInfoComparer)
          .ToList();
      ShowPersons = true;
    }
    else
    {
      Persons = null;
      ShowPersons = false;
    }
  }
}