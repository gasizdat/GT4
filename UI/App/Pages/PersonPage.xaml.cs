using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Formatters;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class PersonPage : ContentPage
{
  private readonly Stack<Person> _PersonBackNavigationStack = [];
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IDataConverter _TextConverter;
  private readonly ICommand _PageCommand;
  private readonly ObservableCollection<RelativeInfo> _Relatives = new();
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;
  private byte[][] _Photos = [];
  private string _Biography = string.Empty;
  private PersonPageSmartLayout _SmartLayout = new();

  protected PersonPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateSpanFormatter = _ServiceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
    _TextConverter = _ServiceProvider.GetRequiredKeyedService<IDataConverter>(DataCategory.PersonBio);
    _PageCommand = new Command<object>(OnPageCommand);

    InitializeComponent();
  }

  public PersonPage()
    : this(ServiceBuilder.DefaultServices)
  {
  }

  public void ShowPersonInfo(Person person)
  {
    Task.Run(async () => await GetPersonDataAsync(person));
  }

  public ICommand PageCommand => _PageCommand;

  public string EditPersonToolbarItemName => string.Format(UIStrings.MenuItemNameEdit_1, ShortName);

  public string RemovePersonToolbarItemName => string.Format(UIStrings.MenuItemNameRemove_1, ShortName);

  public string ShortName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.ShortPersonName);

  public string CommonName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.CommonPersonName);

  public string FullName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.FullPersonName);

  public bool ShowFullName => CommonName != FullName;

  public string BirthDate => _DateFormatter.ToString(_PersonFullInfo.BirthDate);

  public string DeathDate => _DateFormatter.ToString(_PersonFullInfo.DeathDate);

  public bool ShowDeathDate => _PersonFullInfo.DeathDate.HasValue;

  public string Age
  {
    get
    {
      var age = _PersonFullInfo.DeathDate.GetValueOrDefault(Date.Now) - _PersonFullInfo.BirthDate;
      return _DateSpanFormatter.ToString(age);
    }
  }

  public bool ShowSinceBirth
  {
    get
    {
      return _PersonFullInfo.BirthDate.Status < DateStatus.Unknown && _PersonFullInfo.DeathDate.HasValue;
    }
  }

  public string SinceBirth
  {
    get
    {
      var dateSpan = Date.Now - _PersonFullInfo.BirthDate;
      var ret = _DateSpanFormatter.ToString(dateSpan with { Status = DateStatus.DayUnknown});
      ret = string.Format(UIStrings.PersonSinceBirthDay_1, ret);

      return ret;
    }
  }

  public ICollection Relatives => _Relatives;

  public byte[][] Photos => _Photos;

  public PersonFullInfo PersonFullInfo => _PersonFullInfo;

  public PersonInfo PersonInfo
  {
    set => ShowPersonInfo(value);
  }

  public PersonPageSmartLayout SmartLayout => _SmartLayout;

  public string Biography => _Biography;

  public bool ShowBiography => !string.IsNullOrWhiteSpace(_Biography);

  public Name? FamilyName => _PersonFullInfo.Names.SingleOrDefault(n => n.Type == NameType.FamilyName);

  protected override void OnSizeAllocated(double width, double height)
  {
    base.OnSizeAllocated(width, height);
    var widthInPixels = () => width * DeviceDisplay.Current.MainDisplayInfo.Density;
    if (width < height || widthInPixels() < 900)
    {
      _SmartLayout = new PersonPageSmartLayout(
        Image: new GridLayout(Column: 0, ColumnSpan: 2, Row: 0, RowSpan: 1),
        Relatives: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1),
        Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 2, RowSpan: 1));
    }
    else
    {
      _SmartLayout = new PersonPageSmartLayout(
        Image: new GridLayout(Column: 0, ColumnSpan: 1, Row: 0, RowSpan: 1),
        Relatives: new GridLayout(Column: 1, ColumnSpan: 1, Row: 0, RowSpan: 1),
        Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1));
    }
    OnPropertyChanged(nameof(SmartLayout));
  }

  protected override bool OnBackButtonPressed()
  {
    if (_PersonBackNavigationStack.Count > 0)
    {
      var person = _PersonBackNavigationStack.Pop();
      var routeName = GetRoute(person);
      ShowPersonInfo(person);
      return false;
    }

    return base.OnBackButtonPressed();
  }

  private async Task GetPersonDataAsync(Person person)
  {
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      var personFullInfo = await project.PersonManager.GetPersonFullInfoAsync(person, token);
      var parentsTasks = project.RelativesProvider.GetParentsAsync(personFullInfo.RelativeInfos, token);
      var stepChildrenTasks = project.RelativesProvider.GetStepChildrenAsync(personFullInfo.RelativeInfos, token);
      var bioTask = _TextConverter.ToObjectAsync(personFullInfo.Biography, token);
      await Task.WhenAll(parentsTasks, stepChildrenTasks, bioTask);

      byte[][] photos;

      if (personFullInfo.MainPhoto is null)
      {
        if (personFullInfo.AdditionalPhotos.Length != 0)
        {
          throw new ApplicationException("Person photos inconsistency");
        }

        using var readResourceToken = _CancellationTokenProvider.CreateShortOperationCancellationToken();
        var defaultImageResourceName = GetDefaultImageResourceName(personFullInfo.BiologicalSex);
        var defaultPhoto = await ImageUtils.ToBytesAsync(defaultImageResourceName, readResourceToken) ?? [];
        photos = [defaultPhoto];
      }
      else
      {
        photos = [personFullInfo.MainPhoto.Content,
                  ..personFullInfo
                    .AdditionalPhotos
                    .Select(photo => photo.Content)];
      }
      _ = MainThread.InvokeOnMainThreadAsync(
        () => UpdateUI(personFullInfo, parentsTasks.Result, stepChildrenTasks.Result, photos, bioTask.Result as string));
    }
    catch (Exception ex)
    {
      await this.ShowErrorAsync(ex);
      await Shell.Current.GoToAsync("..", true);
      return;
    }
  }

  public void UpdateUI(PersonFullInfo personFullInfo, Parents parents, RelativeInfo[] stepChildren, byte[][] photos, string? bio)
  {
    var relativesProvider = _CurrentProjectProvider.Project.RelativesProvider;
    var siblings = relativesProvider.GetSiblings(personFullInfo, parents);
    _PersonFullInfo = personFullInfo;
    _Photos = photos;
    _Relatives.Clear();
    _Biography = bio ?? string.Empty;

    void Add(IEnumerable<RelativeInfo> relatives)
    {
      foreach (var relative in relatives.OrderBy(r => r.BiologicalSex))
      {
        _Relatives.Add(relative);
      }
    }

    Add(_PersonFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse));
    Add(parents.Native);
    Add(parents.Adoptive);
    Add(parents.Step);
    Add(siblings.Native);
    Add(siblings.ByFather);
    Add(siblings.ByMother);
    Add(siblings.Step);
    Add(siblings.Adoptive);
    Add(relativesProvider.GetChildren(personFullInfo.RelativeInfos));
    Add(relativesProvider.GetAdoptiveChildren(personFullInfo.RelativeInfos));
    Add(stepChildren);

    this.RefreshView();
  }

  private async void OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "RemovePerson":
        // TODO Implement person removing
        break;
      case string commandName when commandName == "EditPerson":
        await OnPersonEdit();
        break;
      case string commandName when commandName == "Refresh":
        PersonInfo = _PersonFullInfo;
        break;
      case string commandName when commandName == "GoToHome":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectPage>());
        break;
      case string commandName when commandName == "GoToFamily":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = FamilyName });
        break;
      case RelativeInfo relativeInfo:
        var nextPerson = ToPerson(relativeInfo);
        var routeName = GetRoute(nextPerson);
        RegisterRoute(routeName, nextPerson);
        _ = Shell.Current.GoToAsync(routeName);
        _PersonBackNavigationStack.Push(ToPerson(_PersonFullInfo));
        break;
    }
  }

  private async Task OnPersonEdit()
  {
    var dialog = new CreateOrUpdatePersonDialog(_PersonFullInfo, _ServiceProvider);
    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null)
      {
        return;
      }

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _CurrentProjectProvider
        .Project
        .PersonManager
        .UpdatePersonAsync(info, token);

      PersonInfo = info;
    }
    catch (Exception ex)
    {
      await this.ShowErrorAsync(ex);
    }
  }

  private static string GetDefaultImageResourceName(BiologicalSex biologicalSex) =>
    biologicalSex switch
    {
      BiologicalSex.Male => "male_stub.png",
      BiologicalSex.Female => "female_stub.png",
      _ => "project_icon.png"
    };

  private static Person ToPerson(Person person)
  {
    var ret = new Person(
      Id: person.Id,
      BirthDate: person.BirthDate,
      DeathDate: person.DeathDate,
      BiologicalSex: person.BiologicalSex);
    return ret;
  }

  private static string GetRoute(Person person)
  {
    const string pidPrefix = "pid";
    var newRoute = Routing.FormatRoute([pidPrefix, $"{person.Id}"]);
    return newRoute;
  }

  private void RegisterRoute(string routeName, Person person)
  {
    Routing.UnRegisterRoute(routeName);
    Routing.RegisterRoute(routeName, new PersonPageRouteFactory(this, person));
  }

  class PersonPageRouteFactory : RouteFactory
  {
    private readonly WeakReference<PersonPage> _PersonPage;
    private readonly Person _Person;

    public PersonPageRouteFactory(PersonPage personPage, Person person)
    {
      _PersonPage = new(personPage);
      _Person = person;
    }

    public override Element GetOrCreate()
    {
      if (_PersonPage.TryGetTarget(out var personPage))
      {
        personPage.ShowPersonInfo(_Person);
        return personPage;
      }

      throw new ApplicationException("Unable to get PersonPage");
    }

    public override Element GetOrCreate(IServiceProvider services)
    {
      return GetOrCreate();
    }
  }
}
