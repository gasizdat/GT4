using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Logic;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Collections;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class PersonPage : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly PersonPageLogic _Logic;
  private readonly ICommand _PageCommand;
  private readonly RelativeTree _Relatives;
  private readonly PersonNavigation _Navigation = new();
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;
  private byte[][] _Photos = [];
  private string _Biography = string.Empty;
  private PersonPageSmartLayout _SmartLayout = new();
  private bool _ExpandAll = false;

  public PersonPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    var currentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _DateSpanFormatter = _ServiceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
    _Logic = _ServiceProvider.GetRequiredService<PersonPageLogic>();
    _PageCommand = new SafeCommand(OnPageCommand);
    _Relatives = new RelativeTree(currentProjectProvider, _CancellationTokenProvider);

    InitializeComponent();
  }

  public void ShowPersonInfo(Person person, bool addToNavigation)
  {
    ExpandAll = false;
    Task.Run(async () => await LoadPersonAsync(person, addToNavigation));
  }

  public bool ExpandAll
  {
    get => _ExpandAll;
    set
    {
      _ExpandAll = value;
      OnPropertyChanged(nameof(ExpandAll));
      OnPropertyChanged(nameof(ToggleAllButtonName));
      OnPropertyChanged(nameof(ToggleAllMenuItemName));
    }
  }

  public string ToggleAllButtonName => ExpandAll ? "⏫" : "⏬";

  public string ToggleAllMenuItemName =>
    string.Format(ExpandAll ? UIStrings.MenuItemCollapseAll_1 : UIStrings.MenuItemExpandAll_1, ToggleAllButtonName);

  public ICommand PageCommand => _PageCommand;

  public string EditPersonToolbarItemName => string.Format(UIStrings.MenuItemNameEdit_1, ShortName);

  public string RemovePersonToolbarItemName => string.Format(UIStrings.MenuItemNameRemove_1, ShortName);

  public string ShortName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.ShortPersonName);

  public string FullName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.FullPersonName);

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
      var ret = _DateSpanFormatter.ToString(dateSpan with { Status = DateStatus.DayUnknown });
      ret = string.Format(UIStrings.PersonSinceBirthDay_1, ret);

      return ret;
    }
  }

  public ICollection Relatives => _Relatives.Rows;

  public byte[][] Photos => _Photos;

  public PersonFullInfo PersonFullInfo => _PersonFullInfo;

  public PersonInfo PersonInfo
  {
    set => ShowPersonInfo(value, true);
  }

  public PersonPageSmartLayout SmartLayout
  {

    get => _SmartLayout;
    set
    {
      if (_SmartLayout != value)
      {
        _SmartLayout = value;
        OnPropertyChanged(nameof(SmartLayout));
      }
    }
  }

  public string Biography => _Biography;

  public bool ShowBiography => !string.IsNullOrWhiteSpace(_Biography);

  public Name? FamilyName => _PersonFullInfo.Names.SingleOrDefault(n => n.Type == NameType.FamilyName);

  public string GoToFamilyName => string.Format(UIStrings.MenuItemGotoFamily_1, FamilyName?.Value ?? string.Empty);

  public ICollection NavigationHistory => _Navigation.History;

  public PersonInfo? CurrentPerson
  {
    get => _Navigation.Current;
    set
    {
      var next = _Navigation.Select(value);
      if (next is not null)
      {
        OnPropertyChanged(nameof(CurrentPerson));
        ShowPersonInfo(next, false);
      }
    }
  }

  protected override void OnSizeAllocated(double width, double height)
  {
    base.OnSizeAllocated(width, height);
    SmartLayout = PersonPageLogic.ComputeLayout(
      width: width,
      height: height,
      density: DeviceDisplay.Current.MainDisplayInfo.Density);
  }

  // Loads a person off the UI thread: PersonLogic fetches the document data and assembles the relatives;
  // the page then resolves photos (default stub image is an app-package resource) and renders the bio/GEDCOM
  // markdown, and finally marshals the UI update onto the main thread.
  private async Task LoadPersonAsync(Person person, bool addToNavigation)
  {
    try
    {
      var data = await _Logic.GetPersonDataAsync(person);
      var photos = await ResolvePhotosAsync(data.PersonFullInfo);
      var biography = await _Logic.CombineBiographyAsync(data.PersonFullInfo);
      // UpdateUI marshals onto the UI thread; SafeTask.RunOnMainThread keeps an escaped exception (e.g. the
      // project closed while backgrounding) from going unobserved.
      _ = SafeTask.RunOnMainThread(() => UpdateUI(data, photos, biography, addToNavigation));
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      // The project was closed underneath us (e.g. the app is backgrounding). Nothing to surface.
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      // LoadPersonAsync runs on a background thread (Task.Run); both the alert and the navigation touch
      // native views, so marshal them onto the UI thread.
      await this.ShowErrorAsync(ex);
      await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("..", true));
    }
  }

  // A person with no main photo shows a sex-specific stub image loaded from the app package; otherwise the
  // main photo leads, followed by any additional photos.
  private async Task<byte[][]> ResolvePhotosAsync(PersonFullInfo personFullInfo)
  {
    var stored = PersonPageLogic.GetStoredPhotoContents(personFullInfo);
    if (stored is not null)
    {
      return stored;
    }

    using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
    var resourceName = GetDefaultImageResourceName(personFullInfo.BiologicalSex);
    var defaultPhoto = await ImageUtils.ToBytesAsync(resourceName, token) ?? [];
    return [defaultPhoto];
  }

  public void UpdateUI(PersonData data, byte[][] photos, string biography, bool addToNavigation)
  {
    _PersonFullInfo = data.PersonFullInfo;
    _Photos = photos;
    _Biography = biography;

    _Relatives.SetRoots(data.Roots, _PersonFullInfo.BirthDate);

    this.RefreshView();

    if (addToNavigation)
    {
      _Navigation.Append(_PersonFullInfo);
      OnPropertyChanged(nameof(CurrentPerson)); // drives CollectionView highlight + ScrollToSelected
    }
  }

  private async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "RemovePerson":
        await OnPersonRemoveAsync();
        break;
      case string commandName when commandName == "EditPerson":
        await OnPersonEditAsync();
        break;
      case string commandName when commandName == "Refresh":
        ShowPersonInfo(_PersonFullInfo, false);
        break;
      case string commandName when commandName == "GoToHome":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<MainPage>());
        break;
      case string commandName when commandName == "GoToFamily":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = FamilyName! });
        break;
      case string commandName when commandName == "GoToFamilyTree":
        // Shell matches the target's [QueryProperty] by exact runtime type, so hand it a plain
        // PersonInfo — passing the PersonFullInfo subclass sends Shell down a Convert.ChangeType path
        // that throws (the object is not IConvertible).
        var centerInfo = new PersonInfo(_PersonFullInfo, _PersonFullInfo.Names, _PersonFullInfo.MainPhoto);
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyTreePage>(), true, new() { ["PersonInfo"] = centerInfo });
        break;
      case RelativeInfo relativeInfo:
        ShowPersonInfo(relativeInfo, true);
        break;
      case string commandName when commandName == "PreviousPerson":
        OnNextPerson(-1);
        break;
      case string commandName when commandName == "NextPerson":
        OnNextPerson(1);
        break;
      case string commandName when commandName == "ToggleAll":
        ExpandAll = !ExpandAll;
        _ = SafeTask.Run(() => _Relatives.ExpandAllAsync(ExpandAll));
        break;
    }
  }

  private void OnNextPerson(int dIndex)
  {
    var person = _Navigation.Move(dIndex);
    if (person is not null)
    {
      OnPropertyChanged(nameof(CurrentPerson));
      ShowPersonInfo(person, false);
    }
  }

  private async Task OnPersonRemoveAsync()
  {
    await _Logic.RemovePersonAsync(_PersonFullInfo);
  }

  private async Task OnPersonEditAsync()
  {
    var dialog = new CreateOrUpdatePersonDialog(_PersonFullInfo, _ServiceProvider);
    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
    {
      return;
    }

    await _Logic.UpdatePersonAsync(info);

    PersonInfo = info;
  }

  private static string GetDefaultImageResourceName(BiologicalSex biologicalSex) =>
    biologicalSex switch
    {
      BiologicalSex.Male => "male_stub.png",
      BiologicalSex.Female => "female_stub.png",
      _ => "project_icon.png"
    };
}
