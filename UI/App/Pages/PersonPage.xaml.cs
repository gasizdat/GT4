using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class PersonPage : ContentPage
{
  private readonly Stack<Person> _PersonBackNavigationStack = [];
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly ICommand _PageCommand;
  private readonly ObservableCollection<RelativeInfoItem> _Relatives = new();
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;
  private byte[][] _Photos = [];

  public PersonPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _RelationshipTypeFormatter = _ServiceProvider.GetRequiredService<IRelationshipTypeFormatter>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateSpanFormatter = _ServiceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
    _PageCommand = new Command(OnPageCommand);

    InitializeComponent();
  }

  public PersonPage()
    : this(ServiceBuilder.DefaultServices)
  {
  }

  public void ShowPersonInfo(Person person)
  {
    using var backgroundWorker = new BackgroundWorker();
    backgroundWorker.DoWork += async (object? _, DoWorkEventArgs args) =>
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      var personFullInfo = await project.PersonManager.GetPersonFullInfoAsync(person, token);
      var siblings = await _CurrentProjectProvider
        .Project
        .PersonManager
        .GetSiblings(personFullInfo, token);
      byte[][] photos;

      if (personFullInfo.MainPhoto is null)
      {
        if (personFullInfo.AdditionalPhotos.Length != 0)
        {
          throw new ApplicationException("Person photos inconsistency");
        }

        using var readResourceToken = _CancellationTokenProvider.CreateShortOperationCancellationToken();
        var defaultImageResourceName = GetDefaultImageResourceName(personFullInfo.BiologicalSex);
        // TODO for some reason await stops this DoWork handler ahead of time and RunWorkerCompleted handler is preliminary invoked.
        var defaultPhoto = ImageUtils.ToBytesAsync(defaultImageResourceName, readResourceToken).Result ?? [];
        photos = [defaultPhoto];
      }
      else
      {
        photos = [personFullInfo.MainPhoto.Content,
                  ..personFullInfo
                    .AdditionalPhotos
                    .Select(photo => photo.Content)];
      }

      args.Result = (personFullInfo, siblings, photos);
    };
    backgroundWorker.RunWorkerCompleted += async (object? _, RunWorkerCompletedEventArgs args) =>
    {
      if (args.Error is not null)
      {
        await PageAlert.ShowError(args.Error);
        await Shell.Current.GoToAsync("..", true);
        return;
      }
      if (args.Cancelled || args.Result is null)
      {
        await PageAlert.ShowConfirmation("Operation cancelled");
        await Shell.Current.GoToAsync("..", true);
        return;
      }

      var (personFullInfo, siblings, photos) = ((PersonFullInfo, Siblings, byte[][]))args.Result;
      _PersonFullInfo = personFullInfo;
      _Photos = photos;
      _Relatives.Clear();

      void Add(IEnumerable<RelativeInfo> relatives)
      {
        foreach (var relative in relatives.OrderBy(r => r.BiologicalSex))
        {
          var relativeInfoItem = new RelativeInfoItem(
            _PersonFullInfo.BirthDate, relative, _DateFormatter, _RelationshipTypeFormatter, _NameFormatter);
          _Relatives.Add(relativeInfoItem);
        }
      }

      Add(_PersonFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spose));
      Add(PersonManager.Parent(_PersonFullInfo));
      Add(PersonManager.AdoptiveParent(personFullInfo));
      Add(siblings.Native);
      Add(siblings.ByFather);
      Add(siblings.ByMother);
      Add(siblings.Step);
      Add(siblings.Adoptive);
      Add(PersonManager.Children(personFullInfo));
      Add(PersonManager.AdoptiveChildren(personFullInfo));

      Utils.RefreshView(this);
    };

    backgroundWorker.RunWorkerAsync();
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

  public ICollection Relatives => _Relatives;

  public byte[][] Photos => _Photos;

  public PersonInfo PersonInfo
  {
    set => ShowPersonInfo(value);
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

  private async void OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string name when name == "RemovePerson":
        break;
      case string name when name == "EditPerson":
        await OnPersonEdit();
        break;
      case string name when name == "Refresh":
        PersonInfo = _PersonFullInfo;
        break;
      case RelativeInfoItem relativeInfoItem:
        var nextPerson = ToPerson(relativeInfoItem.Info);
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
      await this.ShowError(ex);
    }
  }

  private static string GetDefaultImageResourceName(BiologicalSex biologicalSex)=>
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
