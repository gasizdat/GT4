using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Converters;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class PersonPage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IDataConverter _TextConverter;
  private readonly IDataConverter _GedcomConverter;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly ICommand _PageCommand;
  private readonly RelativeTree _Relatives;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private readonly CreateOrUpdatePersonDialog.Factory _CreateOrUpdatePersonDialogFactory;
  private readonly InlineMediaResolver _MediaResolver;
  private ObservableCollection<PersonInfo> _NavigationHistory = new();
  private int _NavigationIndex = -1;
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;
  private PhotoInfo[] _Photos = [];
  private AttachmentInfo[] _Attachments = [];
  private string _Biography = string.Empty;
  private bool _IsNarrow = true;
  private PersonTab _SelectedTab;
  private bool _ExpandAll = false;
  private RelativeInfo[] _AllRoots = [];
  private ProjectInfo? _LastProjectInfo;

  // Relatives first: it is the only one every person has, and so the one anything else falls back to.
  private enum PersonTab
  {
    Relatives,
    Biography,
    Attachments
  }

  public PersonPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IDateSpanFormatter dateSpanFormatter,
    IDateFormatter dateFormatter,
    INameFormatter nameFormatter,
    [FromKeyedServices(DataCategory.PersonBio)]
    IDataConverter textConverter,
    [FromKeyedServices(DataCategory.PersonGedcomTags)]
    IDataConverter gedcomConverter,
    DataConverterResolver dataConverterResolver,
    IAlertService alertService,
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter,
    CreateOrUpdatePersonDialog.Factory createOrUpdatePersonDialogFactory,
    InlineMediaProvider mediaProvider
    )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _DateSpanFormatter = dateSpanFormatter;
    _DateFormatter = dateFormatter;
    _NameFormatter = nameFormatter;
    _TextConverter = textConverter;
    _GedcomConverter = gedcomConverter;
    _DataConverterResolver = dataConverterResolver;
    _AlertService = alertService;
    _NavigationService = navigationService;
    _CreateOrUpdatePersonDialogFactory = createOrUpdatePersonDialogFactory;
    _MediaResolver = mediaProvider.ResolveAsync;
    Loading = new PageLoading(_AlertService);
    _PageCommand = new SafeCommand(OnPageCommand, _AlertService);
    _Relatives = new RelativeTree(_CurrentProjectProvider, _CancellationTokenProvider, _AlertService);

    InitializeComponent();

    FilterView.Initialize(
      biologicalSexFormatter,
      _CancellationTokenProvider,
      _CurrentProjectProvider,
      _AlertService,
      () => _AllRoots);
    FilterView.Changed += (_, _) => RefreshRelatives();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  protected ImagePresenter PersonPhotoView => _IsNarrow ? ScrolledPersonPhoto : PersonPhoto;

  protected CollectionView RelativesListView => RelativesView;

  protected ScrollView BiographyScrollView => BiographyView;

  public void ShowPersonInfo(Person person, bool addToNavigation)
  {
    ExpandAll = false;
    Loading.Run(_PersonFullInfo.Id != ElementId.NonCommittedId, () => GetPersonDataAsync(person, addToNavigation));
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

  public PageLoading Loading { get; }

  public string ToggleAllButtonName => ExpandAll ? "⏫" : "⏬";

  public string ToggleAllMenuItemName =>
    string.Format(ExpandAll ? UIStrings.MenuItemCollapseAll_1 : UIStrings.MenuItemExpandAll_1, ToggleAllButtonName);

  private void RefreshRelatives() => _Relatives.SetFilter(FilterView.IsAnyFilterActive, r => FilterView.Matches(r));

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

  public PhotoInfo[] Photos => _Photos;

  /// <summary>One photo, caption stripped: at thumbnail size ImagePresenter's caption bar and its
  /// prev/next arrows are each wider than the picture, and each is suppressed by what it is given
  /// here.</summary>
  public PhotoInfo[] HeaderPhoto => _Photos.Length > 0 ? [_Photos[0] with { Caption = string.Empty }] : [];

  public InlineMediaResolver MediaResolver => _MediaResolver;

  public AttachmentInfo[] Attachments => _Attachments;

  public bool ShowAttachments => _Attachments.Length != 0;

  public PersonFullInfo PersonFullInfo => _PersonFullInfo;

  public PersonInfo PersonInfo
  {
    set => ShowPersonInfo(value, true);
  }

  public bool ShowRelativesTab => _SelectedTab == PersonTab.Relatives;

  public bool ShowBiographyTab => _SelectedTab == PersonTab.Biography;

  public bool ShowAttachmentsTab => _SelectedTab == PersonTab.Attachments;

  /// <summary>Whether the strip is worth its space: with nothing beside the relatives there is no
  /// choice to offer, only a lone tab that cannot be left.</summary>
  public bool ShowTabs => ShowBiography || ShowAttachments;

  public bool IsNarrowLayout => _IsNarrow;

  public bool IsWideLayout => !_IsNarrow;

  private bool IsTabAvailable(PersonTab tab) => tab switch
  {
    PersonTab.Biography => ShowBiography,
    PersonTab.Attachments => ShowAttachments,
    _ => true
  };

  private void SelectTab(PersonTab tab)
  {
    _SelectedTab = tab;
    OnPropertyChanged(nameof(ShowRelativesTab));
    OnPropertyChanged(nameof(ShowBiographyTab));
    OnPropertyChanged(nameof(ShowAttachmentsTab));
  }

  public string Biography => _Biography;

  public bool ShowBiography => !string.IsNullOrWhiteSpace(_Biography);

  // The biography block doubles as the home for the read-only GEDCOM details: the stored bio first, then
  // the person's own residual tags, then their couples', so a person carrying only imported GEDCOM data
  // still shows the block.
  private static string CombineBiography(params string?[] sections)
  {
    var present = sections.Where(section => !string.IsNullOrWhiteSpace(section));
    return string.Join("\n\n", present);
  }

  public Name FamilyName =>
    _PersonFullInfo.Names.SingleOrDefault(n => n.Type == NameType.FamilyName, FamilyInfoItem.NoFamilyName);

  public ICollection NavigationHistory => _NavigationHistory;

  public PersonInfo? CurrentPerson
  {
    get => _NavigationIndex >= 0 ? _NavigationHistory[_NavigationIndex] : null;
    set
    {
      if (value is null)
      {
        return;
      }

      var index = _NavigationHistory.IndexOf(value);
      if (index != _NavigationIndex)
      {
        _NavigationIndex = index;
        OnPropertyChanged(nameof(CurrentPerson));
        ShowPersonInfo(value, false);
      }
    }
  }

  protected override void OnSizeAllocated(double width, double height)
  {
    base.OnSizeAllocated(width, height);
    var widthInPixels = () => width * DeviceDisplay.Current.MainDisplayInfo.Density;
    _IsNarrow = width < height || widthInPixels() < 900;
    OnPropertyChanged(nameof(IsNarrowLayout));
    OnPropertyChanged(nameof(IsWideLayout));
  }

  // See ProjectPage.OnNavigatedTo: returning from a subpage (family, tree, another person) that
  // committed an edit must re-fetch the current person's data.
  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      Refresh();
    }
  }

  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    ShowPersonInfo(_PersonFullInfo, false);
  }

  private void OnPersonLinkTapped(object? sender, int personId) =>
    SafeTask.Run(() => NavigateToPersonLinkAsync(personId), _AlertService);

  // A dangling link (the referenced person was since deleted) is simply inert.
  protected async Task NavigateToPersonLinkAsync(int personId)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var person = await _CurrentProjectProvider.Project.Persons.TryGetPersonByIdAsync(personId, token);
    if (person is not null)
    {
      ShowPersonInfo(person, true);
    }
  }

  private void OnAttachmentLinkTapped(object? sender, int attachmentId) =>
    SafeTask.Run(() => OpenAttachmentLinkAsync(attachmentId), _AlertService);

  // A dangling link (the referenced attachment was since removed) is simply inert.
  protected async Task OpenAttachmentLinkAsync(int attachmentId)
  {
    var attachment = _Attachments.FirstOrDefault(a => a.Data.Id == attachmentId);
    attachment ??= await TryResolveAttachmentAsync(attachmentId);
    if (attachment is not null)
    {
      await OnOpenAttachmentAsync(attachment);
    }
  }

  protected async Task<AttachmentInfo?> TryResolveAttachmentAsync(int attachmentId)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var data = await _CurrentProjectProvider.Project.Data.TryGetDataByIdAsync(attachmentId, token);
    if (data is null)
    {
      return null;
    }

    var content = await _DataConverterResolver(data.Category).ToObjectAsync(data, token);
    return content as AttachmentInfo;
  }

  private async Task OnOpenAttachmentAsync(AttachmentInfo attachment)
  {
    using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
    await attachment.OpenAsync(token);
  }

  private async Task OnGotoFamilyAsync() =>
    await _NavigationService.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = FamilyName });

  private async Task GetPersonDataAsync(Person person, bool addToNavigation)
  {
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      var startInfo = _CurrentProjectProvider.Info;
      var personFullInfo = await project.PersonManager.GetPersonFullInfoAsync(person, token);
      var parentsTasks = project.RelativesProvider.GetParentsAsync(personFullInfo.RelativeInfos, token);
      var stepChildrenTasks = project.RelativesProvider.GetStepChildrenAsync(personFullInfo.RelativeInfos, token);
      var bioTask = _TextConverter.ToObjectAsync(personFullInfo.Biography, token);
      var gedcomTask = _GedcomConverter.ToObjectAsync(personFullInfo.GedcomData, token);
      var attachmentsTask = Task.WhenAll(
        personFullInfo.Attachments.Select(data => _DataConverterResolver(data.Category).ToObjectAsync(data, token)));
      await Task.WhenAll(parentsTasks, stepChildrenTasks, bioTask, gedcomTask, attachmentsTask);

      // Sequenced after the attachments: a couple's media renders under the name the attachment row carries.
      var attachments = attachmentsTask.Result.OfType<AttachmentInfo>().ToArray();
      var familyDetails = await PersonFamilyDetails.ReadAsync(project, personFullInfo, attachments, _NameFormatter, token);

      var parents = parentsTasks.Result;
      var stepChildren = stepChildrenTasks.Result;
      var relativesProvider = project.RelativesProvider;
      var siblings = relativesProvider.GetSiblings(personFullInfo, parents);
      var roots = AssembleRoots(personFullInfo, parents, siblings, stepChildren, relativesProvider);
      var photos = await LoadPhotosAsync(personFullInfo, token);
      var data = new PersonPageData(
        personFullInfo,
        roots,
        photos,
        attachments,
        bioTask.Result as string,
        gedcomTask.Result as string,
        familyDetails,
        addToNavigation);

      // UpdateUI touches the project document again on the UI thread; SafeTask.RunOnMainThread keeps
      // an escaped exception (e.g. the project closed while backgrounding) from going unobserved.
      _ = SafeTask.RunOnMainThread(() =>
      {
        if (startInfo != _CurrentProjectProvider.Info)
        {
          Refresh();
          return;
        }

        UpdateUI(data);
      }, _AlertService);
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      // The project was closed underneath us (e.g. the app is backgrounding). Nothing to surface.
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      // GetPersonDataAsync runs on a background thread (Task.Run); both the alert and the
      // navigation touch native views, so marshal them onto the UI thread.
      await _AlertService.ShowErrorAsync(ex);
      await MainThread.InvokeOnMainThreadAsync(() => _NavigationService.GoToAsync("..", true));
      return;
    }
  }

  private async Task<PhotoInfo[]> LoadPhotosAsync(PersonFullInfo personFullInfo, CancellationToken token)
  {
    PhotoInfo GetDefaultPhotoInfo() => new(ImageUtils.ImageFromRawResource(
      ImageUtils.DefaultPersonPhotoResourceName(personFullInfo.BiologicalSex), null), null);

    Data[] photoData = personFullInfo.MainPhoto is null
      ? [.. personFullInfo.AdditionalPhotos]
      : [personFullInfo.MainPhoto, .. personFullInfo.AdditionalPhotos];
    var photos = (await Task.WhenAll(photoData.Select(data => _DataConverterResolver(data.Category).ToObjectAsync(data, token))))
      .Select(obj => obj is PhotoInfo photoInfo ? photoInfo : GetDefaultPhotoInfo())
      .ToArray();

    // PersonPage always shows one photo (unlike FamilyPage, which hides its panel instead), so a person
    // with no photos at all still needs the biological-sex stub.
    if (photos.Length == 0)
    {
      photos = [GetDefaultPhotoInfo()];
    }

    return photos;
  }

  private static RelativeInfo[] AssembleRoots(
    PersonFullInfo personFullInfo,
    Parents parents,
    Siblings siblings,
    RelativeInfo[] stepChildren,
    IRelativesProvider relativesProvider)
  {
    var roots = new List<RelativeInfo>();
    void Add(IEnumerable<RelativeInfo> relatives) => roots.AddRange(relatives.OrderBy(r => r.BiologicalSex));

    Add(personFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse));
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

    return [.. roots];
  }

  private void UpdateUI(PersonPageData data)
  {
    _PersonFullInfo = data.PersonFullInfo;
    _Photos = data.Photos;
    _Attachments = data.Attachments;
    _Biography = CombineBiography(data.Bio, data.GedcomDetails, data.FamilyDetails);
    _AllRoots = data.Roots;
    // A tab is hidden for a person without its content, which would leave the body blank and unleavable.
    if (!IsTabAvailable(_SelectedTab))
    {
      SelectTab(PersonTab.Relatives);
    }

    // Only after the new roots land: with the panel open, ResetFilterData re-fetches immediately,
    // snapshotting the page's current person set.
    FilterView.ResetFilterData();

    _Relatives.SetRoots(_AllRoots, _PersonFullInfo.BirthDate);

    this.RefreshView();

    if (data.AddToNavigation)
    {
      AddToNavigation(_PersonFullInfo);
    }
  }

  private void AddToNavigation(PersonInfo personInfo)
  {
    var personInfoCopy = new PersonInfo(personInfo, names: personInfo.Names, mainPhoto: personInfo.MainPhoto);

    // Re-showing the person already selected in history (e.g. after editing it) must update that
    // entry in place; otherwise every edit/refresh of the current person pushes a duplicate.
    if (_NavigationIndex >= 0 && _NavigationHistory[_NavigationIndex].Id == personInfoCopy.Id)
    {
      _NavigationHistory[_NavigationIndex] = personInfoCopy;
      OnPropertyChanged(nameof(CurrentPerson));
      return;
    }

    var newIndex = _NavigationIndex + 1;
    while (newIndex < _NavigationHistory.Count)
    {
      _NavigationHistory.RemoveAt(newIndex);
    }
    _NavigationHistory.Add(personInfoCopy);

    // Not routed through the CurrentPerson setter: its data was just fetched by the caller of
    // AddToNavigation, so re-entering ShowPersonInfo here would re-run the whole fetch pipeline for
    // the same person a second time. OnNextPerson still goes through the setter, which does need a
    // fresh fetch for an already-visited (lightweight) history entry.
    _NavigationIndex = newIndex;
    OnPropertyChanged(nameof(CurrentPerson));
  }

  protected async Task OnPageCommand(object obj)
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
      case string commandName when commandName == "GoToFamilyTree":
        // Shell matches the target's [QueryProperty] by exact runtime type, so hand it a plain
        // PersonInfo — passing the PersonFullInfo subclass sends Shell down a Convert.ChangeType path
        // that throws (the object is not IConvertible).
        var centerInfo = new PersonInfo(_PersonFullInfo, _PersonFullInfo.Names, _PersonFullInfo.MainPhoto);
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<FamilyTreePage>(), true, new() { ["PersonInfo"] = centerInfo });
        break;
      case RelativeInfo relativeInfo:
        ShowPersonInfo(relativeInfo, true);
        break;
      case AttachmentInfo attachment:
        await OnOpenAttachmentAsync(attachment);
        break;
      case string commandName when commandName == "PreviousPerson":
        OnNextPerson(-1);
        break;
      case string commandName when commandName == "NextPerson":
        OnNextPerson(1);
        break;
      case string commandName when commandName == "TabRelatives":
        SelectTab(PersonTab.Relatives);
        break;
      case string commandName when commandName == "TabBiography":
        SelectTab(PersonTab.Biography);
        break;
      case string commandName when commandName == "TabAttachments":
        SelectTab(PersonTab.Attachments);
        break;
      case string commandName when commandName == "ToggleAll":
        ExpandAll = !ExpandAll;
        _ = SafeTask.Run(() => _Relatives.ExpandAllAsync(ExpandAll), _AlertService);
        break;
    }
  }

  private void OnNextPerson(int dIndex)
  {
    var person = _NavigationHistory.ElementAtOrDefault(_NavigationIndex + dIndex);
    if (person is not null)
    {
      CurrentPerson = person;
    }
  }

  private async Task OnPersonRemoveAsync()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .Persons
      .RemovePersonAsync(_PersonFullInfo, token);

    await OnGotoFamilyAsync();
  }

  private async Task OnPersonEditAsync()
  {
    var dialog = _CreateOrUpdatePersonDialogFactory.Create(_PersonFullInfo);
    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

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
}
